// File: src/NetworkChecker/Diagnostics/VerdictCalculator.cs
using NetworkChecker.Core;

namespace NetworkChecker.Diagnostics;

/// <summary>
/// 仕様書 4.3 / 6 の判定ルールに基づく総合判定。
/// 障害は断定せず「可能性」として提示し、複数の検査結果を組み合わせる。
/// </summary>
public static class VerdictCalculator
{
    public static void Evaluate(DiagnosticReport report, DiagnosticContext ctx)
    {
        var status = report.Checks
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.First().Status, StringComparer.OrdinalIgnoreCase);

        bool Failed(string id) => status.TryGetValue(id, out var s) && s == CheckStatus.Failed;
        bool Ok(string id) => status.TryGetValue(id, out var s) && s == CheckStatus.Ok;
        bool Warned(string id) => status.TryGetValue(id, out var s) && s == CheckStatus.Warning;

        string code;
        int exitCode;
        var causes = new List<string>();
        var hints = new List<string>();

        // 上流（端末側）から順に根本原因を評価する
        if (Failed("NET-001"))
        {
            code = "ADAPTER_DOWN"; exitCode = 2;
            causes.Add("cause.ADAPTER_DOWN");
            hints.Add("hint.CHECK_WIFI");
        }
        else if (Failed("NET-002"))
        {
            code = "NO_IP_ADDRESS"; exitCode = 2;
            causes.Add("cause.NO_IP");
            hints.Add("hint.REBOOT_ROUTER");
            hints.Add("hint.CHECK_WIFI");
        }
        else if (Failed("NET-003"))
        {
            code = "NO_GATEWAY"; exitCode = 2;
            causes.Add("cause.NO_GATEWAY");
            hints.Add("hint.REBOOT_ROUTER");
        }
        else if (Failed("NET-004"))
        {
            code = "GATEWAY_UNREACHABLE"; exitCode = 2;
            causes.Add("cause.GATEWAY");
            hints.Add("hint.REBOOT_ROUTER");
            hints.Add("hint.CHECK_WIFI");
        }
        else if (Failed("DNS-001"))
        {
            code = "NO_DNS_CONFIGURED"; exitCode = 2;
            causes.Add("cause.NO_DNS");
            hints.Add("hint.REBOOT_ROUTER");
        }
        else if (Failed("DNS-003"))
        {
            if (Ok("DNS-004"))
            {
                // 使用DNSのみ失敗 → 使用DNSまたは経路に問題の可能性
                code = "DNS_SUSPECTED"; exitCode = 3;
                causes.Add("cause.DNS_SUSPECTED");
                hints.Add("hint.REBOOT_ROUTER");
                hints.Add("hint.WAIT_ISP");
                hints.Add("hint.DNS_CHANGE");
            }
            else if (Failed("WAN-001"))
            {
                // 両方失敗かつ外部IPも不通 → 上位回線の可能性
                code = "WAN_DOWN"; exitCode = 2;
                causes.Add("cause.WAN");
                hints.Add("hint.REBOOT_ROUTER");
                hints.Add("hint.WAIT_ISP");
            }
            else
            {
                // 両方失敗だが外部IPは到達可能 → UDP/TCP 53 遮断等の可能性
                code = "DNS_FILTERED"; exitCode = 2;
                causes.Add("cause.DNS_FILTERED");
                hints.Add("hint.CHECK_SECURITY");
                if (ctx.ProxyEnabled) causes.Add("cause.PROXY");
            }
        }
        else if (Failed("WAN-001"))
        {
            code = "WAN_DOWN"; exitCode = 2;
            causes.Add("cause.WAN");
            hints.Add("hint.REBOOT_ROUTER");
            hints.Add("hint.WAIT_ISP");
        }
        else if (Failed("WAN-002"))
        {
            // DNS成功・HTTPS失敗 → サービス、TLS、プロキシ、FW等の可能性
            code = "HTTPS_SUSPECTED"; exitCode = 2;
            causes.Add("cause.HTTPS");
            if (ctx.TimeWarning)
            {
                causes.Add("cause.CLOCK");
                hints.Add("hint.CHECK_TIME");
            }
            if (ctx.ProxyEnabled) causes.Add("cause.PROXY");
            hints.Add("hint.CHECK_SECURITY");
        }
        else if (Failed("APP-001") || Warned("APP-001"))
        {
            // 基準 HTTPS は正常で特定サービスのみ異常 → サービス側の可能性
            code = "SERVICE_DEGRADED"; exitCode = 1;
            causes.Add("cause.SERVICE");
            hints.Add("hint.CHECK_SERVICE");
        }
        else if (status.Values.Any(s => s == CheckStatus.Warning))
        {
            code = "HEALTHY_WITH_WARNINGS"; exitCode = 1;
            if (ctx.TimeWarning)
            {
                causes.Add("cause.CLOCK");
                hints.Add("hint.CHECK_TIME");
            }
        }
        else
        {
            code = "HEALTHY"; exitCode = 0;
        }

        report.VerdictCode = code;
        report.ExitCode = exitCode;
        report.VerdictMessage = Strings.Get($"verdict.{code}");
        report.SuspectedCauses.AddRange(causes.Select(Strings.Get).Distinct());
        report.Hints.AddRange(hints.Select(Strings.Get).Distinct());
    }
}