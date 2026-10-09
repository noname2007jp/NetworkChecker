// File: src/NetworkChecker/Diagnostics/HttpProbe.cs
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkChecker.Diagnostics;

public sealed class HttpProbeResult
{
    /// <summary>TLS 確立と HTTP 応答の取得に成功したか（HTTP エラーステータスでも true）。</summary>
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public long? DnsMs { get; set; }
    public long? TcpMs { get; set; }
    public long? TlsMs { get; set; }
    public long? HttpMs { get; set; }
    public long TotalMs { get; set; }
    public int RedirectCount { get; set; }
    public string? FinalUrl { get; set; }
    public string? ConnectedAddress { get; set; }
    public string? ErrorCategory { get; set; } // DNS / REFUSED / TCP / TLS / HTTP / TIMEOUT / URL
    public string? ErrorDetail { get; set; }
    public string? RedirectLocation { get; set; }
    public string? CertSubject { get; set; }
    public string? CertIssuer { get; set; }
    public DateTimeOffset? CertNotAfter { get; set; }
    public bool? CertChainOk { get; set; }
    public string? CertErrors { get; set; }
    public bool? CertHostnameMismatch { get; set; }
    public DateTimeOffset? ServerDate { get; set; }
}

/// <summary>
/// DNS 解決・TCP 接続・TLS ハンドシェイク・HTTP 応答を個別に計測するプローブ。
/// </summary>
public static class HttpProbe
{
    private const int MaxRedirects = 5;

    public static async Task<HttpProbeResult> ProbeAsync(
        string url, int timeoutMs, bool validateCertificate, bool enableIpv6, CancellationToken ct)
    {
        var current = url;
        for (var hop = 0; ; hop++)
        {
            var result = await ProbeOnceAsync(current, timeoutMs, validateCertificate, enableIpv6, ct);
            result.RedirectCount = hop;

            if (!result.Success) return result;

            var isRedirect = result.StatusCode is 301 or 302 or 303 or 307 or 308;
            if (isRedirect && result.RedirectLocation is not null && hop < MaxRedirects)
            {
                current = new Uri(new Uri(current), result.RedirectLocation).ToString();
                continue;
            }

            result.FinalUrl = current;
            return result;
        }
    }

    private static async Task<HttpProbeResult> ProbeOnceAsync(
        string url, int timeoutMs, bool validateCertificate, bool enableIpv6, CancellationToken ct)
    {
        var result = new HttpProbeResult();
        var total = Stopwatch.StartNew();

        Uri uri;
        try { uri = new Uri(url); }
        catch
        {
            result.ErrorCategory = "URL";
            result.ErrorDetail = url;
            return result;
        }

        var useTls = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var port = uri.IsDefaultPort ? (useTls ? 443 : 80) : uri.Port;
        var hostHeader = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        var path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        var token = cts.Token;

        try
        {
            // ---- DNS 解決（実環境の挙動を見るため OS リゾルバを使用）
            var sw = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, token);
            if (!enableIpv6)
            {
                addresses = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
            }
            if (addresses.Length == 0)
            {
                result.ErrorCategory = "DNS";
                result.ErrorDetail = "no address";
                return result;
            }
            result.DnsMs = sw.ElapsedMilliseconds;

            // ---- TCP 接続
            TcpClient? tcp = null;
            Exception? lastError = null;
            sw.Restart();
            foreach (var address in addresses)
            {
                token.ThrowIfCancellationRequested();
                var candidate = new TcpClient(address.AddressFamily);
                try
                {
                    await candidate.ConnectAsync(address, port, token);
                    tcp = candidate;
                    result.TcpMs = sw.ElapsedMilliseconds;
                    result.ConnectedAddress = address.ToString();
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    candidate.Dispose();
                }
            }
            if (tcp is null)
            {
                result.ErrorCategory = lastError is SocketException { SocketErrorCode: SocketError.ConnectionRefused }
                    ? "REFUSED" : "TCP";
                result.ErrorDetail = lastError?.Message;
                return result;
            }

            using (tcp)
            {
                Stream stream = tcp.GetStream();
                SslStream? ssl = null;

                // ---- TLS ハンドシェイク
                if (useTls)
                {
                    sw.Restart();
                    var callbackInvoked = false;
                    var capturedErrors = SslPolicyErrors.None;
                    X509Certificate2? remoteCert = null;

                    ssl = new SslStream(stream, leaveInnerStreamOpen: false, (sender, certificate, chain, errors) =>
                    {
                        // 検証結果を記録するため一旦すべて受け入れ、後段で判定する
                        callbackInvoked = true;
                        capturedErrors = errors;
                        if (certificate is X509Certificate2 x2) remoteCert = x2;
                        else if (certificate is not null) remoteCert = new X509Certificate2(certificate);
                        return true;
                    });

                    try
                    {
                        await ssl.AuthenticateAsClient(new SslClientAuthenticationOptions
                        {
                            TargetHost = uri.Host,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        }, token);
                    }
                    catch (Exception ex)
                    {
                        ssl.Dispose();
                        result.ErrorCategory = "TLS";
                        result.ErrorDetail = ex.Message;
                        return result;
                    }

                    result.TlsMs = sw.ElapsedMilliseconds;
                    result.CertChainOk = callbackInvoked && capturedErrors == SslPolicyErrors.None;
                    result.CertErrors = result.CertChainOk == true ? null : capturedErrors.ToString();
                    result.CertHostnameMismatch = capturedErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch);
                    if (remoteCert is not null)
                    {
                        result.CertSubject = remoteCert.Subject;
                        result.CertIssuer = remoteCert.Issuer;
                        result.CertNotAfter = remoteCert.NotAfter;
                    }
                    if (validateCertificate && result.CertChainOk == false)
                    {
                        ssl.Dispose();
                        result.ErrorCategory = "TLS";
                        result.ErrorDetail = result.CertErrors ?? "certificate validation failed";
                        return result;
                    }
                    stream = ssl;
                }

                // ---- HTTP 応答
                try
                {
                    sw.Restart();
                    var request =
                        $"GET {path} HTTP/1.1\r\n" +
                        $"Host: {hostHeader}\r\n" +
                        "User-Agent: NetworkChecker/1.0\r\n" +
                        "Accept: */*\r\n" +
                        "Connection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(request), token);
                    var headerBytes = await ReadHeadersAsync(stream, token);
                    result.HttpMs = sw.ElapsedMilliseconds;
                    ParseHeaders(headerBytes, result);
                    result.Success = result.StatusCode is not null;
                    if (!result.Success)
                    {
                        result.ErrorCategory = "HTTP";
                        result.ErrorDetail = "invalid response";
                    }
                }
                finally
                {
                    ssl?.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            result.ErrorCategory = "TIMEOUT";
            result.ErrorDetail = $"timeout ({timeoutMs} ms)";
        }
        catch (SocketException ex)
        {
            result.ErrorCategory = ex.SocketErrorCode == SocketError.ConnectionRefused ? "REFUSED" : "TCP";
            result.ErrorDetail = ex.Message;
        }
        catch (Exception ex)
        {
            result.ErrorCategory = "UNKNOWN";
            result.ErrorDetail = ex.Message;
        }
        finally
        {
            total.Stop();
            result.TotalMs = total.ElapsedMilliseconds;
        }

        return result;
    }

    private static async Task<byte[]> ReadHeadersAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        while (ms.Length < 65536)
        {
            var n = await stream.ReadAsync(buffer, ct);
            if (n == 0) break;
            ms.Write(buffer, 0, n);
            if (ContainsHeaderEnd(ms.GetBuffer(), (int)ms.Length)) break;
        }
        return ms.ToArray();
    }

    private static bool ContainsHeaderEnd(byte[] data, int length)
    {
        for (var i = 0; i + 3 < length; i++)
        {
            if (data[i] == (byte)'\r' && data[i + 1] == (byte)'\n' &&
                data[i + 2] == (byte)'\r' && data[i + 3] == (byte)'\n')
            {
                return true;
            }
        }
        return false;
    }

    private static void ParseHeaders(byte[] data, HttpProbeResult result)
    {
        var text = Encoding.ASCII.GetString(data);
        var lines = text.Split("\r\n");
        if (lines.Length == 0 || !lines[0].StartsWith("HTTP/", StringComparison.Ordinal)) return;

        var parts = lines[0].Split(' ', 3);
        if (parts.Length >= 2 && int.TryParse(parts[1], out var code)) result.StatusCode = code;
        if (parts.Length >= 3) result.ReasonPhrase = parts[2];

        foreach (var line in lines.Skip(1))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var name = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            if (name.Equals("Location", StringComparison.OrdinalIgnoreCase))
            {
                result.RedirectLocation = value;
            }
            else if (name.Equals("Date", StringComparison.OrdinalIgnoreCase) &&
                     DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                         DateTimeStyles.AssumeUniversal, out var date))
            {
                result.ServerDate = date;
            }
        }
    }
}