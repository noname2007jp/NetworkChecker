// File: src/NetworkChecker/Diagnostics/DnsClient.cs
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkChecker.Diagnostics;

public enum DnsRecordType : ushort
{
    A = 1,
    AAAA = 28,
}

public sealed record DnsQueryResult
{
    /// <summary>サーバーから何らかの応答があったか（RCODE がエラーでも true）。</summary>
    public bool Responded { get; init; }
    public int ResponseCode { get; init; } = -1;
    public string ResponseCodeName { get; init; } = "";
    public IPAddress[] Addresses { get; init; } = Array.Empty<IPAddress>();
    public long LatencyMs { get; init; }
    public string QueryName { get; init; } = "";
    public string QueryType { get; init; } = "";
    public string? Error { get; init; }
    public bool UsedTcp { get; init; }
}

/// <summary>
/// UDP/TCP 53 を直接使用する DNS クライアント。
/// OS リゾルバを経由しないため、使用 DNS と比較用 DNS を分けて判定できる。
/// </summary>
public static class DnsClient
{
    public static async Task<DnsQueryResult> QueryAsync(
        IPAddress server, string name, DnsRecordType type, int timeoutMs, CancellationToken ct)
    {
        var query = BuildQuery(name, (ushort)type, out var id);
        var sw = Stopwatch.StartNew();

        try
        {
            byte[] response;
            using (var udp = new UdpClient(server.AddressFamily))
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);
                await udp.SendAsync(query, new IPEndPoint(server, 53), cts.Token);
                var received = await udp.ReceiveAsync(cts.Token);
                response = received.Buffer;
            }

            var parsed = ParseResponse(response, id);
            if (!parsed.Truncated)
            {
                return ToResult(parsed.Rcode, parsed.Addresses, sw.ElapsedMilliseconds, name, type, usedTcp: false);
            }

            // TC ビット: TCP で再問い合わせ
            var remaining = Math.Max(1000, timeoutMs - (int)sw.ElapsedMilliseconds);
            response = await QueryTcpRawAsync(server, query, remaining, ct);
            var tcpParsed = ParseResponse(response, id);
            return ToResult(tcpParsed.Rcode, tcpParsed.Addresses, sw.ElapsedMilliseconds, name, type, usedTcp: true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure("timeout");
        }
        catch (SocketException ex)
        {
            return Failure(ex.SocketErrorCode.ToString());
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }

        DnsQueryResult Failure(string error) => new()
        {
            Responded = false,
            Error = error,
            LatencyMs = sw.ElapsedMilliseconds,
            QueryName = name,
            QueryType = type.ToString(),
        };
    }

    private static DnsQueryResult ToResult(
        int rcode, IPAddress[] addresses, long elapsedMs, string name, DnsRecordType type, bool usedTcp)
        => new()
        {
            Responded = true,
            ResponseCode = rcode,
            ResponseCodeName = RcodeName(rcode),
            Addresses = addresses,
            LatencyMs = elapsedMs,
            QueryName = name,
            QueryType = type.ToString(),
            UsedTcp = usedTcp,
        };

    private static byte[] BuildQuery(string name, ushort qtype, out ushort id)
    {
        id = (ushort)Random.Shared.Next(0, 65536);
        using var ms = new MemoryStream();

        void W16(ushort value)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, value);
            ms.Write(b);
        }

        W16(id);
        W16(0x0100); // RD=1
        W16(1);      // QDCOUNT
        W16(0);
        W16(0);
        W16(0);

        foreach (var label in name.TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes);
        }
        ms.WriteByte(0);
        W16(qtype);
        W16(1); // IN
        return ms.ToArray();
    }

    private static (int Rcode, IPAddress[] Addresses, bool Truncated) ParseResponse(byte[] buf, ushort expectedId)
    {
        if (buf.Length < 12) throw new InvalidDataException("short response");
        if (BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(0)) != expectedId)
            throw new InvalidDataException("id mismatch");

        var flags = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(2));
        var truncated = (flags & 0x0200) != 0;
        var rcode = flags & 0x000F;
        var qdCount = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(4));
        var anCount = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(6));

        var offset = 12;
        for (var i = 0; i < qdCount; i++)
        {
            offset = SkipName(buf, offset);
            offset += 4;
        }

        var addresses = new List<IPAddress>();
        for (var i = 0; i < anCount; i++)
        {
            if (offset >= buf.Length) break;
            offset = SkipName(buf, offset);
            if (offset + 10 > buf.Length) break;

            var type = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(offset));
            var rdLength = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(offset + 8));
            var rdata = offset + 10;
            if (rdata + rdLength > buf.Length) break;

            if (type == (ushort)DnsRecordType.A && rdLength == 4)
                addresses.Add(new IPAddress(buf.AsSpan(rdata, 4)));
            else if (type == (ushort)DnsRecordType.AAAA && rdLength == 16)
                addresses.Add(new IPAddress(buf.AsSpan(rdata, 16)));

            offset = rdata + rdLength;
        }

        return (rcode, addresses.ToArray(), truncated);
    }

    private static int SkipName(byte[] buf, int offset)
    {
        while (offset < buf.Length)
        {
            var length = buf[offset];
            if (length == 0) return offset + 1;
            if ((length & 0xC0) == 0xC0) return offset + 2; // 圧縮ポインタ
            offset += 1 + length;
        }
        return offset;
    }

    private static async Task<byte[]> QueryTcpRawAsync(
        IPAddress server, byte[] query, int timeoutMs, CancellationToken ct)
    {
        using var tcp = new TcpClient(server.AddressFamily);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        await tcp.ConnectAsync(server, 53, cts.Token);
        var stream = tcp.GetStream();

        var framed = new byte[query.Length + 2];
        BinaryPrimitives.WriteUInt16BigEndian(framed.AsSpan(0), (ushort)query.Length);
        query.CopyTo(framed, 2);
        await stream.WriteAsync(framed, cts.Token);

        var lengthBuffer = await ReadExactlyAsync(stream, 2, cts.Token);
        var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBuffer);
        return await ReadExactlyAsync(stream, length, cts.Token);
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int length, CancellationToken ct)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), ct);
            if (n == 0) throw new EndOfStreamException();
            offset += n;
        }
        return buffer;
    }

    private static string RcodeName(int rcode) => rcode switch
    {
        0 => "NOERROR",
        1 => "FORMERR",
        2 => "SERVFAIL",
        3 => "NXDOMAIN",
        5 => "REFUSED",
        _ => $"RCODE{rcode}",
    };
}