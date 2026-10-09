// File: src/NetworkChecker/Diagnostics/Checks.cs
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkChecker.Core;

namespace NetworkChecker.Diagnostics;

/// <summary>仕様書の診断項目（NET/DNS/WAN/APP/SYS）の実装。</summary>
public static class Checks
{
    // ---------------- NET-001 ネットワークアダプター ----------------
    public static Task<CheckResult> Net001AdaptersAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("NET-001");

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Where(a => a.GetIPProperties().UnicastAddresses.Any(u =>
                u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address)))
            .ToList();

        ctx.ActiveAdapters.Clear();
        ctx.ActiveAdapters.AddRange(adapters);

        ctx.PrimaryAdapter = adapters
            .OrderByDescending(a => a.GetIPProperties().GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address)))
            .ThenByDescending(a => a.Speed)
            .FirstOrDefault();

        if (ctx.PrimaryAdapter is null)
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("detail.noAdapter");
            return Task.FromResult(result);
        }

        var p = ctx.PrimaryAdapter;
        ctx.AdapterTypeName = AdapterTypeName(p);
        ctx.AdapterName = p.Name;

        result.Status = CheckStatus.Ok;
        result.Detail = ctx.AdapterTypeName;
        result.Evidence.Add($"Adapter: {p.Name} ({p.Description})");
        var mac = p.GetPhysicalAddress().GetAddressBytes();
        if (mac.Length > 0) result.Evidence.Add($"MAC: {PrivacyMask.Mac(mac)}");
        result.Evidence.Add($"Speed: {p.Speed / 1_000_000} Mbps");
        return Task.FromResult(result);
    }

    // ---------------- NET-002 IPアドレス ----------------
    public static Task<CheckResult> Net002IpAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("NET-002");
        var p = ctx.PrimaryAdapter;
        if (p is null) return Task.FromResult(Skip(result, "skip.dependency"));

        var props = p.GetIPProperties();
        var v4 = props.UnicastAddresses
            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address))
            .Select(u => u.Address)
            .ToList();
        var v6 = ctx.Config.EnableIpv6
            ? props.UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                            !u.Address.IsIPv6LinkLocal && !IPAddress.IsLoopback(u.Address))
                .Select(u => u.Address)
                .ToList()
            : new List<IPAddress>();

        ctx.LocalIPv4.AddRange(v4);
        ctx.LocalIPv6.AddRange(v6);

        if (v4.Count == 0 && v6.Count == 0)
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("detail.noIp");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Ok;
        var parts = v4.Select(a => $"IPv4 {PrivacyMask.Ip(a)}").ToList();
        parts.AddRange(v6.Select(a => $"IPv6 {PrivacyMask.Ip(a)}"));
        result.Detail = string.Join(", ", parts);
        return Task.FromResult(result);
    }

    // ---------------- NET-003 デフォルトゲートウェイ ----------------
    public static Task<CheckResult> Net003GatewayAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("NET-003");
        var p = ctx.PrimaryAdapter;
        if (p is null) return Task.FromResult(Skip(result, "skip.dependency"));

        var gateways = p.GetIPProperties().GatewayAddresses
            .Select(g => g.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                        (ctx.Config.EnableIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6))
            .Where(a => !IPAddress.Any.Equals(a) && !IPAddress.IPv6Any.Equals(a))
            .Distinct()
            .ToList();

        ctx.Gateways.AddRange(gateways);

        if (gateways.Count == 0)
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("detail.noGateway");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Ok;
        result.Detail = PrivacyMask.Ip(gateways[0]);
        foreach (var g in gateways) result.Evidence.Add($"Gateway: {PrivacyMask.Ip(g)}");
        return Task.FromResult(result);
    }

    // ---------------- NET-004 ゲートウェイ疎通（ICMP / ARP） ----------------
    public static async Task<CheckResult> Net004GatewayPingAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("NET-004");
        if (ctx.Gateways.Count == 0) return Skip(result, "skip.dependency");

        var attempts = 0;
        foreach (var gateway in ctx.Gateways)
        {
            for (var i = 0; i < ctx.Config.RetryCount; i++)
            {
                attempts++;
                var ms = await PingOnceAsync(gateway, ctx.Config.TimeoutMs);
                var method = "ICMP";

                if (ms is null && gateway.AddressFamily == AddressFamily.InterNetwork && TryArp(gateway, out var mac))
                {
                    // ICMP がブロックされていても ARP 解決できれば L2 到達性あり
                    ms = 0;
                    method = "ARP";
                    result.Evidence.Add($"ARP: {PrivacyMask.Mac(mac)}");
                }

                if (ms is not null)
                {
                    result.Status = CheckStatus.Ok;
                    result.Detail = $"{PrivacyMask.Ip(gateway)} ({method})";
                    result.LatencyMs = ms;
                    result.Attempts = attempts;
                    ctx.GatewayOk = true;
                    return result;
                }

                if (i + 1 < ctx.Config.RetryCount)
                    await Task.Delay(ctx.Config.RetryIntervalMs, ct);
            }
        }

        result.Status = CheckStatus.Failed;
        result.Detail = Strings.Get("err.timeout");
        result.Attempts = attempts;
        return result;
    }

    // ---------------- DNS-001 使用DNS取得 ----------------
    public static Task<CheckResult> Dns001ServersAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("DNS-001");
        var p = ctx.PrimaryAdapter;
        if (p is null) return Task.FromResult(Skip(result, "skip.dependency"));

        List<IPAddress> Collect(NetworkInterface ni)
            => ni.GetIPProperties().DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                            (ctx.Config.EnableIpv6 && a.AddressFamily == AddressFamily.InterNetworkV6))
                .Where(a => !IPAddress.Any.Equals(a) && !IPAddress.IPv6Any.Equals(a))
                .ToList();

        var dns = Collect(p);
        if (dns.Count == 0)
        {
            foreach (var adapter in ctx.ActiveAdapters.Where(a => !ReferenceEquals(a, p)))
                dns.AddRange(Collect(adapter));
            dns = dns.Distinct().ToList();
        }

        ctx.SystemDns.AddRange(dns);

        if (dns.Count == 0)
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("detail.noDns");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Ok;
        result.Detail = PrivacyMask.Ip(dns[0]);
        foreach (var d in dns) result.Evidence.Add($"DNS: {PrivacyMask.Ip(d)}");

        var suffix = p.GetIPProperties().DnsSuffix;
        if (!string.IsNullOrEmpty(suffix))
            result.Evidence.Add($"Suffix: {PrivacyMask.Name(suffix)}");
        return Task.FromResult(result);
    }

    // ---------------- DNS-002 DNSサーバー疎通（UDP/TCP 53） ----------------
    public static async Task<CheckResult> Dns002ServerPingAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("DNS-002");
        if (ctx.SystemDns.Count == 0) return Skip(result, "skip.dependency");

        var attempts = 0;
        var reachable = new List<(IPAddress Server, long Ms)>();
        var unreachable = new List<IPAddress>();

        foreach (var server in ctx.SystemDns.Take(2))
        {
            var ok = false;
            for (var i = 0; i < ctx.Config.RetryCount && !ok; i++)
            {
                attempts++;
                var r = await DnsClient.QueryAsync(server, ctx.Config.TestDomain, DnsRecordType.A, ctx.Config.TimeoutMs, ct);
                if (r.Responded)
                {
                    reachable.Add((server, r.LatencyMs));
                    ok = true;
                }
                else if (i + 1 < ctx.Config.RetryCount)
                {
                    await Task.Delay(ctx.Config.RetryIntervalMs, ct);
                }
            }
            if (!ok) unreachable.Add(server);
        }

        result.Attempts = attempts;
        ctx.SystemDnsResponsive = reachable.Count > 0;

        if (reachable.Count > 0)
        {
            result.Status = unreachable.Count > 0 ? CheckStatus.Warning : CheckStatus.Ok;
            result.LatencyMs = reachable.Min(x => x.Ms);
            result.Detail = $"{PrivacyMask.Ip(reachable[0].Server)}";
            foreach (var (s, ms) in reachable) result.Evidence.Add($"{PrivacyMask.Ip(s)}: {ms} ms");
            foreach (var s in unreachable) result.Evidence.Add($"{PrivacyMask.Ip(s)}: {Strings.Get("err.noResponse")}");
        }
        else
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("err.noResponse");
        }
        return result;
    }

    // ---------------- DNS-003 DNS名前解決（A/AAAA） ----------------
    public static async Task<CheckResult> Dns003ResolutionAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("DNS-003");
        if (ctx.SystemDns.Count == 0) return Skip(result, "skip.dependency");

        var server = ctx.SystemDns[0];
        var attempts = 0;
        DnsQueryResult? aRes = null;
        DnsQueryResult? aaaaRes = null;

        for (var i = 0; i < ctx.Config.RetryCount; i++)
        {
            attempts++;
            aRes = await DnsClient.QueryAsync(server, ctx.Config.TestDomain, DnsRecordType.A, ctx.Config.TimeoutMs, ct);
            if (ctx.Config.EnableIpv6)
                aaaaRes = await DnsClient.QueryAsync(server, ctx.Config.TestDomain, DnsRecordType.AAAA, ctx.Config.TimeoutMs, ct);

            var done = (aRes.Responded && aRes.ResponseCode == 0) ||
                       (aaaaRes is { Responded: true, ResponseCode: 0 });
            if (done) break;
            if (i + 1 < ctx.Config.RetryCount) await Task.Delay(ctx.Config.RetryIntervalMs, ct);
        }

        result.Attempts = attempts;
        ctx.DnsResolutionAttempted = true;

        var aOk = aRes is { Responded: true, ResponseCode: 0 } && aRes.Addresses.Length > 0;
        var aaaaOk = aaaaRes is { Responded: true, ResponseCode: 0 } && aaaaRes.Addresses.Length > 0;
        ctx.NameResolutionOk = aOk || aaaaOk;

        if (aRes is not null)
        {
            result.Evidence.Add($"{PrivacyMask.Ip(server)} A: {(aRes.Responded ? aRes.ResponseCodeName : aRes.Error)} {aRes.LatencyMs} ms");
            ctx.ResolvedBySystemDns.AddRange(aRes.Addresses);
        }
        if (aaaaRes is not null)
        {
            result.Evidence.Add($"{PrivacyMask.Ip(server)} AAAA: {(aaaaRes.Responded ? aaaaRes.ResponseCodeName : aaaaRes.Error)} {aaaaRes.LatencyMs} ms");
            ctx.ResolvedBySystemDns.AddRange(aaaaRes.Addresses);
        }

        if (ctx.NameResolutionOk)
        {
            result.Status = CheckStatus.Ok;
            result.LatencyMs = Math.Min(aOk ? aRes!.LatencyMs : long.MaxValue, aaaaOk ? aaaaRes!.LatencyMs : long.MaxValue);
            // 解決結果の IP は公開情報のためマスクしない
            var resolved = ctx.ResolvedBySystemDns.Take(3).Select(a => a.ToString());
            result.Detail = $"{ctx.Config.TestDomain} → {string.Join(", ", resolved)}";
        }
        else
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("err.nameResolution");
        }
        return result;
    }

    // ---------------- DNS-004 外部DNS比較 ----------------
    public static async Task<CheckResult> Dns004CompareAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("DNS-004");
        var servers = ParseIpList(ctx.Config.ReferenceDnsServers);
        if (servers.Count == 0)
        {
            result.Status = CheckStatus.Unknown;
            result.Detail = Strings.Get("skip.dependency");
            return result;
        }

        var attempts = 0;
        var okList = new List<(IPAddress Server, long Ms, IPAddress[] Addresses)>();

        foreach (var server in servers)
        {
            var done = false;
            for (var i = 0; i < ctx.Config.RetryCount && !done; i++)
            {
                attempts++;
                var r = await DnsClient.QueryAsync(server, ctx.Config.TestDomain, DnsRecordType.A, ctx.Config.TimeoutMs, ct);
                if (r.Responded)
                {
                    // 比較用 DNS は公開サーバーのためマスクしない
                    result.Evidence.Add($"{server}: {r.ResponseCodeName} {r.LatencyMs} ms" +
                                        (r.Addresses.Length > 0 ? $" → {string.Join(",", r.Addresses.Take(2).Select(a => a.ToString()))}" : ""));
                    if (r.ResponseCode == 0) okList.Add((server, r.LatencyMs, r.Addresses));
                    done = true;
                }
                else if (i + 1 < ctx.Config.RetryCount)
                {
                    await Task.Delay(ctx.Config.RetryIntervalMs, ct);
                }
            }
            if (!done) result.Evidence.Add($"{server}: {Strings.Get("err.noResponse")}");
        }

        result.Attempts = attempts;
        ctx.ReferenceDnsResponsive = okList.Count > 0;

        if (okList.Count > 0)
        {
            result.Status = CheckStatus.Ok;
            result.LatencyMs = okList.Min(x => x.Ms);
            result.Detail = string.Join(" / ", okList.Take(2).Select(x => $"{x.Server} ({x.Ms} ms)"));

            // 使用DNSとの応答差異（CDN 等で起こりうるため注記のみ）
            if (ctx.NameResolutionOk && ctx.ResolvedBySystemDns.Count > 0)
            {
                var sysSet = new HashSet<string>(ctx.ResolvedBySystemDns.Select(a => a.ToString()));
                var refSet = new HashSet<string>(okList.SelectMany(x => x.Addresses).Select(a => a.ToString()));
                if (!sysSet.Overlaps(refSet))
                    result.Evidence.Add(Strings.Get("evidence.dnsMismatch"));
            }
        }
        else
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("err.noResponse");
        }
        return result;
    }

    // ---------------- WAN-001 外部IP到達（DNS 不使用） ----------------
    public static async Task<CheckResult> Wan001ExternalIpAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("WAN-001");
        var targets = ParseIpList(ctx.Config.ExternalPingTargets);
        if (targets.Count == 0)
        {
            result.Status = CheckStatus.Unknown;
            result.Detail = Strings.Get("skip.dependency");
            return result;
        }

        var attempts = 0;
        foreach (var ip in targets)
        {
            for (var i = 0; i < ctx.Config.RetryCount; i++)
            {
                attempts++;
                var ms = await PingOnceAsync(ip, ctx.Config.TimeoutMs);
                var method = "ICMP";
                if (ms is null)
                {
                    // ICMP ブロック環境を考慮し TCP 443 で代替確認
                    ms = await TcpConnectMsAsync(ip, 443, ctx.Config.TimeoutMs, ct);
                    method = "TCP443";
                }

                if (ms is not null)
                {
                    result.Status = CheckStatus.Ok;
                    result.Detail = $"{ip} ({method})";
                    result.LatencyMs = ms;
                    result.Attempts = attempts;
                    result.Evidence.Add($"{ip}: {ms} ms via {method}");
                    ctx.ExternalIpOk = true;
                    return result;
                }

                if (i + 1 < ctx.Config.RetryCount)
                    await Task.Delay(ctx.Config.RetryIntervalMs, ct);
            }
        }

        result.Status = CheckStatus.Failed;
        result.Detail = Strings.Get("err.timeout");
        result.Attempts = attempts;
        return result;
    }

    // ---------------- WAN-002 HTTPS接続（基準URL） ----------------
    public static async Task<CheckResult> Wan002HttpsAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("WAN-002");
        if (!ctx.Config.EnableHttpTest) return Skip(result, "skip.disabled");
        if (ctx.DnsResolutionAttempted && !ctx.NameResolutionOk) return Skip(result, "skip.dnsFailed");

        var r = await HttpProbe.ProbeAsync(
            ctx.Config.HttpsBaselineUrl, ctx.Config.TimeoutMs,
            ctx.Config.EnableCertificateTest, ctx.Config.EnableIpv6, ct);

        ctx.BaselineHttps = r;
        ctx.HttpsBaselineOk = r.Success;
        if (r.ServerDate is not null) ctx.ReferenceTime ??= r.ServerDate;

        FillHttpEvidence(result, r);

        if (r.Success)
        {
            result.Status = CheckStatus.Ok;
            result.Detail = $"HTTP {r.StatusCode}";
            result.LatencyMs = r.TotalMs;
            // HTTP エラーでもサーバー応答がある＝インターネット接続不可とは判定しない
            if (r.StatusCode >= 400) result.Evidence.Add(Strings.Get("evidence.httpErrorButResponded"));
        }
        else
        {
            result.Status = CheckStatus.Failed;
            result.Detail = r.ErrorCategory switch
            {
                "DNS" => Strings.Get("err.nameResolution"),
                "REFUSED" => Strings.Get("err.refused"),
                "TLS" => Strings.Get("err.tls"),
                _ => Strings.Get("err.timeout"),
            };
        }
        return result;
    }

    // ---------------- APP-001 指定サービス ----------------
    public static async Task<CheckResult> App001ServicesAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("APP-001");
        var targets = ctx.Targets.Where(t => t.Enabled).ToList();
        if (targets.Count == 0) return Skip(result, "skip.noTargets");
        if (!ctx.Config.EnableHttpTest) return Skip(result, "skip.disabled");
        if (ctx.DnsResolutionAttempted && !ctx.NameResolutionOk) return Skip(result, "skip.dnsFailed");

        var ok = 0;
        var fail = 0;
        long worst = 0;

        foreach (var t in targets)
        {
            var timeout = t.TimeoutMs > 0 ? t.TimeoutMs : ctx.Config.TimeoutMs;
            var r = await HttpProbe.ProbeAsync(t.Url, timeout, ctx.Config.EnableCertificateTest, ctx.Config.EnableIpv6, ct);
            if (r.ServerDate is not null) ctx.ReferenceTime ??= r.ServerDate;

            var expected = t.ExpectedStatus is { Length: > 0 } ? t.ExpectedStatus : new[] { 200, 301, 302 };
            if (r.Success && r.StatusCode is not null && expected.Contains(r.StatusCode.Value))
            {
                ok++;
                result.Evidence.Add($"[OK] {t.Name}: HTTP {r.StatusCode} {r.TotalMs} ms");
            }
            else if (r.Success)
            {
                // サーバー応答はあるため通信断とはみなさない
                fail++;
                result.Evidence.Add($"[NG] {t.Name}: HTTP {r.StatusCode} (expected: {string.Join("/", expected)}) — {Strings.Get("evidence.serverResponded")}");
            }
            else
            {
                fail++;
                result.Evidence.Add($"[NG] {t.Name}: {r.ErrorCategory} {r.ErrorDetail}");
            }
            worst = Math.Max(worst, r.TotalMs);
        }

        ctx.ServiceOkCount = ok;
        ctx.ServiceFailCount = fail;
        result.LatencyMs = worst;

        if (fail == 0)
        {
            result.Status = CheckStatus.Ok;
            result.Detail = Strings.Format("detail.servicesOk", ok);
        }
        else if (ok > 0)
        {
            result.Status = CheckStatus.Warning;
            result.Detail = Strings.Format("detail.servicesPartial", ok, ok + fail);
        }
        else
        {
            result.Status = CheckStatus.Failed;
            result.Detail = Strings.Get("detail.servicesAllFailed");
        }
        return result;
    }

    // ---------------- SYS-001 時刻確認（NTP / HTTP Date） ----------------
    public static async Task<CheckResult> Sys001TimeAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("SYS-001");

        DateTimeOffset? reference = null;
        string? source = null;

        foreach (var server in ctx.Config.NtpServers)
        {
            try
            {
                reference = await QueryNtpAsync(server, ctx.Config.TimeoutMs, ct);
                if (reference is not null)
                {
                    source = $"NTP ({server})";
                    break;
                }
            }
            catch
            {
                // 次のサーバーへ
            }
        }

        if (reference is null && ctx.ReferenceTime is not null)
        {
            reference = ctx.ReferenceTime;
            source = "HTTP Date";
        }

        if (reference is null)
        {
            result.Status = CheckStatus.Unknown;
            result.Detail = Strings.Get("detail.timeUnknown");
            return result;
        }

        var skew = (DateTimeOffset.UtcNow - reference.Value.ToUniversalTime()).TotalSeconds;
        ctx.ClockSkewSeconds = skew;
        result.Evidence.Add($"Source: {source}");
        result.Evidence.Add($"Skew: {skew:F1} s");

        if (Math.Abs(skew) > 300)
        {
            ctx.TimeWarning = true;
            result.Status = CheckStatus.Warning;
            result.Detail = Strings.Format("detail.timeSkew", skew.ToString("F0"));
        }
        else
        {
            result.Status = CheckStatus.Ok;
            result.Detail = Strings.Format("detail.timeOk", Math.Abs(skew).ToString("F1"));
        }
        return result;
    }

    // ---------------- SYS-002 プロキシ確認 ----------------
    public static Task<CheckResult> Sys002ProxyAsync(DiagnosticContext ctx, CancellationToken ct)
    {
        var result = NewResult("SYS-002");

        if (!OperatingSystem.IsWindows())
        {
            result.Status = CheckStatus.Unknown;
            result.Detail = Strings.Get("err.permission");
            return Task.FromResult(result);
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var enabled = key?.GetValue("ProxyEnable") is int v && v == 1;
            var server = key?.GetValue("ProxyServer") as string;
            var autoConfig = key?.GetValue("AutoConfigURL") as string;

            ctx.ProxyEnabled = enabled || !string.IsNullOrEmpty(autoConfig);

            if (enabled)
            {
                ctx.ProxyDetail = PrivacyMask.ProxyServer(server);
                result.Status = CheckStatus.Ok;
                result.Detail = Strings.Format("detail.proxyOn", ctx.ProxyDetail);
            }
            else if (!string.IsNullOrEmpty(autoConfig))
            {
                ctx.ProxyDetail = "PAC";
                result.Status = CheckStatus.Ok;
                result.Detail = Strings.Get("detail.proxyPac");
            }
            else
            {
                result.Status = CheckStatus.Ok;
                result.Detail = Strings.Get("detail.proxyOff");
            }
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Unknown;
            result.Detail = Strings.Get("err.permission");
            result.Evidence.Add(ex.Message);
        }
        return Task.FromResult(result);
    }

    // ---------------- helpers ----------------

    private static CheckResult NewResult(string id) => new() { Id = id };

    private static CheckResult Skip(CheckResult result, string reasonKey)
    {
        result.Status = CheckStatus.Skipped;
        result.Detail = Strings.Get(reasonKey);
        return result;
    }

    private static List<IPAddress> ParseIpList(IEnumerable<string> values)
        => values
            .Select(s => IPAddress.TryParse(s.Trim(), out var ip) ? ip : null)
            .Where(ip => ip is not null)
            .Cast<IPAddress>()
            .ToList();

    private static string AdapterTypeName(NetworkInterface ni) => ni.NetworkInterfaceType switch
    {
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT
            => Strings.Get("adapter.ethernet"),
        _ => ni.NetworkInterfaceType.ToString(),
    };

    private static async Task<long?> PingOnceAsync(IPAddress ip, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, timeoutMs);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<long?> TcpConnectMsAsync(IPAddress ip, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var tcp = new TcpClient(ip.AddressFamily);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            var sw = Stopwatch.StartNew();
            await tcp.ConnectAsync(ip, port, cts.Token);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryArp(IPAddress ip, out byte[] mac)
    {
        mac = Array.Empty<byte>();
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var buffer = new byte[6];
            uint length = 6;
            var dest = BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
            if (NativeMethods.SendARP(dest, 0, buffer, ref length) == 0 && length > 0)
            {
                mac = buffer[..(int)length];
                return true;
            }
        }
        catch
        {
            // ARP 確認不可でも診断は継続
        }
        return false;
    }

    private static async Task<DateTimeOffset?> QueryNtpAsync(string host, int timeoutMs, CancellationToken ct)
    {
        var packet = new byte[48];
        packet[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0) return null;

        using var udp = new UdpClient(addresses[0].AddressFamily);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        await udp.SendAsync(packet, new IPEndPoint(addresses[0], 123), cts.Token);
        var response = await udp.ReceiveAsync(cts.Token);
        var data = response.Buffer;
        if (data.Length < 48) return null;

        ulong ReadUInt32(int o)
            => ((ulong)data[o] << 24) | ((ulong)data[o + 1] << 16) | ((ulong)data[o + 2] << 8) | data[o + 3];

        var seconds = ReadUInt32(40); // Transmit Timestamp
        if (seconds == 0) return null;

        var epoch = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return epoch.AddSeconds(seconds);
    }

    private static void FillHttpEvidence(CheckResult result, HttpProbeResult r)
    {
        if (r.DnsMs is not null) result.Evidence.Add($"DNS: {r.DnsMs} ms");
        if (r.TcpMs is not null) result.Evidence.Add($"TCP: {r.TcpMs} ms");
        if (r.TlsMs is not null) result.Evidence.Add($"TLS: {r.TlsMs} ms");
        if (r.HttpMs is not null) result.Evidence.Add($"HTTP: {r.HttpMs} ms");
        if (r.RedirectCount > 0) result.Evidence.Add($"Redirects: {r.RedirectCount}");
        if (r.ConnectedAddress is not null) result.Evidence.Add($"Connected: {r.ConnectedAddress}");
        if (r.CertSubject is not null) result.Evidence.Add($"Cert: {r.CertSubject}");
        if (r.CertIssuer is not null) result.Evidence.Add($"Issuer: {r.CertIssuer}");
        if (r.CertNotAfter is not null)
        {
            result.Evidence.Add($"Cert expires: {r.CertNotAfter:yyyy-MM-dd}");
            var remain = r.CertNotAfter.Value - DateTimeOffset.Now;
            if (remain.TotalDays < 30)
                result.Evidence.Add(Strings.Format("evidence.certExpiring", ((int)remain.TotalDays).ToString()));
        }
        if (r.CertErrors is not null) result.Evidence.Add($"Cert errors: {r.CertErrors}");
        if (r.CertHostnameMismatch == true) result.Evidence.Add(Strings.Get("evidence.certHostname"));
        if (r.StatusCode is not null) result.Evidence.Add($"Status: {r.StatusCode} {r.ReasonPhrase}");
        if (r.ErrorCategory is not null) result.Evidence.Add($"Error: {r.ErrorCategory} {r.ErrorDetail}");
    }
}