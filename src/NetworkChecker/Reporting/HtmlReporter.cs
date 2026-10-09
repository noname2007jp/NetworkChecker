// File: src/NetworkChecker/Reporting/HtmlReporter.cs
using System.Globalization;
using System.Text;
using NetworkChecker.Core;

namespace NetworkChecker.Reporting;

/// <summary>
/// 単一 HTML のレポートを生成する。
/// 初期状態ではプライバシー情報をマスク表示し、「マスク解除」ボタンでローカル内のみ切替可能。
/// 外部への送信は行わない。
/// </summary>
public static class HtmlReporter
{
    public static void Save(DiagnosticReport report, IReadOnlyList<string> logLines, string path, int refreshSeconds)
    {
        var ja = report.Language != "en";

        static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        // マスク表記を <span data-full="原文"> に置き換える（JS で表示切替）
        string Secret(string encoded)
        {
            foreach (var (masked, original) in PrivacyMask.Mappings)
            {
                if (encoded.Contains(masked, StringComparison.Ordinal))
                {
                    var span = $"<span class=\"secret\" data-full=\"{Enc(original)}\">{masked}</span>";
                    encoded = encoded.Replace(masked, span, StringComparison.Ordinal);
                }
            }
            return encoded;
        }

        var bannerClass = report.ExitCode switch
        {
            0 => "ok",
            1 => "warn",
            _ => "fail",
        };
        var bannerLabel = report.ExitCode switch
        {
            0 => "OK",
            1 => "WARNING",
            _ => "FAILED",
        };

        var rows = new StringBuilder();
        foreach (var c in report.Checks)
        {
            var (cls, mark, label) = c.Status switch
            {
                CheckStatus.Ok => ("st-ok", "✔", "OK"),
                CheckStatus.Warning => ("st-warn", "⚠", "WARNING"),
                CheckStatus.Failed => ("st-fail", "✖", "FAILED"),
                CheckStatus.Skipped => ("st-skip", "―", "SKIPPED"),
                _ => ("st-unknown", "?", "UNKNOWN"),
            };
            var detail = c.Detail;
            if (c.Attempts > 1) detail += $" ({Strings.Format("CliAttempts", c.Attempts)})";
            var evidence = c.Evidence.Count == 0
                ? ""
                : $"<details><summary>{Enc(ja ? "詳細" : "Details")}</summary><ul>" +
                  string.Join("", c.Evidence.Select(e => $"<li>{Secret(Enc(e))}</li>")) + "</ul></details>";

            rows.AppendLine($"""
                  <tr>
                    <td><span class="badge {cls}">{mark} {label}</span></td>
                    <td>{Enc(c.Id)}</td>
                    <td>{Enc(c.Name)}</td>
                    <td>{Secret(Enc(detail))}{evidence}</td>
                    <td class="num">{(c.LatencyMs is null ? "" : c.LatencyMs + " ms")}</td>
                  </tr>
                """);
        }

        var causes = report.SuspectedCauses.Count == 0
            ? ""
            : $"<h2>{Enc(ja ? "推定原因" : "Suspected causes")}</h2><ul>" +
              string.Join("", report.SuspectedCauses.Select(x => $"<li>{Enc(x)}</li>")) + "</ul>";
        var hints = report.Hints.Count == 0
            ? ""
            : $"<h2>{Enc(ja ? "対処のヒント" : "Hints")}</h2><ul>" +
              string.Join("", report.Hints.Select(x => $"<li>{Enc(x)}</li>")) + "</ul>";

        var env = report.Environment;
        var envRows = new StringBuilder();
        void EnvRow(string label, string? masked, string? full)
        {
            if (masked is null) return;
            var cell = full is not null && full != masked
                ? $"<span class=\"secret\" data-full=\"{Enc(full)}\">{Enc(masked)}</span>"
                : Enc(masked);
            envRows.AppendLine($"      <tr><th>{Enc(label)}</th><td>{cell}</td></tr>");
        }
        EnvRow("OS", env.OsVersion, null);
        EnvRow(ja ? "コンピューター名" : "Computer", env.ComputerName, env.ComputerNameFull);
        EnvRow(ja ? "ユーザー名" : "User", env.UserName, env.UserNameFull);
        EnvRow(ja ? "接続種別" : "Connection", env.AdapterType, null);
        EnvRow("IPv6", env.Ipv6Enabled ? "ON" : "OFF", null);
        if (env.Proxy is not null) EnvRow(ja ? "プロキシ" : "Proxy", env.Proxy, null);

        var logHtml = string.Join("\n", logLines.Select(l => Enc(l)));
        var duration = (report.FinishedAt - report.StartedAt).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        var startedAt = report.StartedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        var refresh = refreshSeconds > 0 ? $"<meta http-equiv=\"refresh\" content=\"{refreshSeconds}\">" : "";

        var html = $$"""
<!DOCTYPE html>
<html lang="{{(ja ? "ja" : "en")}}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{Enc(ja ? "通信状態チェッカー 診断レポート" : "Network Checker Report")}}</title>
{{refresh}}
<style>
body { font-family: "Yu Gothic UI", "Segoe UI", "Hiragino Sans", sans-serif; margin: 0; background: #f6f8fa; color: #1f2328; }
.container { max-width: 880px; margin: 24px auto; padding: 0 16px 48px; }
h1 { font-size: 20px; margin: 8px 0 4px; }
h2 { font-size: 15px; margin: 20px 0 6px; }
.meta { color: #57606a; font-size: 13px; }
.toolbar { margin: 12px 0; }
button { font-size: 13px; padding: 6px 14px; border: 1px solid #d0d7de; border-radius: 6px; background: #fff; cursor: pointer; margin-right: 8px; }
button:hover { background: #f3f4f6; }
.banner { border-radius: 8px; padding: 14px 18px; font-size: 16px; font-weight: 600; margin: 8px 0; }
.banner small { display: block; font-weight: 400; font-size: 12px; margin-top: 2px; }
.banner.ok { background: #dafbe1; color: #1a7f37; border: 1px solid #aceebb; }
.banner.warn { background: #fff8c5; color: #9a6700; border: 1px solid #f0e09c; }
.banner.fail { background: #ffebe9; color: #cf222e; border: 1px solid #ffc1bc; }
table { border-collapse: collapse; width: 100%; background: #fff; border: 1px solid #d0d7de; border-radius: 8px; overflow: hidden; font-size: 13px; }
th, td { padding: 8px 10px; border-bottom: 1px solid #eaeef2; text-align: left; vertical-align: top; }
tr:last-child td, tr:last-child th { border-bottom: none; }
th { background: #f6f8fa; font-weight: 600; }
td.num { text-align: right; white-space: nowrap; }
.badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 12px; font-weight: 600; white-space: nowrap; }
.st-ok { background: #dafbe1; color: #1a7f37; }
.st-warn { background: #fff8c5; color: #9a6700; }
.st-fail { background: #ffebe9; color: #cf222e; }
.st-skip { background: #ddf4ff; color: #0969da; }
.st-unknown { background: #eaeef2; color: #57606a; }
.secret { border-bottom: 1px dashed #9a6700; }
details { margin-top: 4px; }
summary { cursor: pointer; color: #0969da; font-size: 12px; }
pre { background: #0d1117; color: #e6edf3; padding: 12px; border-radius: 8px; font-size: 12px; overflow-x: auto; white-space: pre-wrap; }
.disclaimer { color: #57606a; font-size: 12px; margin-top: 16px; }
ul { margin: 4px 0; padding-left: 20px; }
</style>
</head>
<body>
<div class="container">
  <h1>{{Enc(ja ? "通信状態チェッカー 診断レポート" : "Network Checker Diagnostic Report")}}</h1>
  <div class="meta">
    {{Enc(ja ? "実施日時" : "Run at")}}: {{startedAt}} / {{Enc(ja ? "所要" : "Duration")}}: {{duration}} s / v{{Enc(report.ToolVersion)}}
  </div>
  <div class="toolbar">
    <button id="privacyBtn" data-show="{{Enc(ja ? "マスク解除" : "Unmask")}}" data-hide="{{Enc(ja ? "マスクする" : "Mask")}}" onclick="togglePrivacy()">{{Enc(ja ? "マスク解除" : "Unmask")}}</button>
    <button onclick="window.print()">{{Enc(ja ? "印刷" : "Print")}}</button>
  </div>
  <div class="banner {{bannerClass}}">
    {{Enc(report.VerdictMessage)}}
    <small>{{bannerLabel}} ({{Enc(report.VerdictCode)}})</small>
  </div>

  <h2>{{Enc(ja ? "検査項目" : "Checks")}}</h2>
  <table>
    <thead><tr><th>{{Enc(ja ? "状態" : "Status")}}</th><th>ID</th><th>{{Enc(ja ? "項目" : "Check")}}</th><th>{{Enc(ja ? "詳細" : "Detail")}}</th><th>{{Enc(ja ? "応答時間" : "Latency")}}</th></tr></thead>
    <tbody>
{{rows}}
    </tbody>
  </table>

  {{causes}}
  {{hints}}

  <h2>{{Enc(ja ? "環境" : "Environment")}}</h2>
  <table>
    <tbody>
{{envRows}}
    </tbody>
  </table>

  <h2>{{Enc(ja ? "詳細ログ" : "Detailed log")}}</h2>
  <details><summary>{{Enc(ja ? "ログを表示" : "Show log")}}</summary><pre>{{logHtml}}</pre></details>

  <div class="disclaimer">{{Enc(Strings.Get("Disclaimer"))}}</div>
</div>
<script>
function togglePrivacy() {
  document.querySelectorAll('.secret').forEach(function (e) {
    var current = e.textContent;
    e.textContent = e.getAttribute('data-full');
    e.setAttribute('data-full', current);
  });
  var b = document.getElementById('privacyBtn');
  var show = b.getAttribute('data-show'), hide = b.getAttribute('data-hide');
  b.textContent = (b.textContent === show) ? hide : show;
}
</script>
</body>
</html>
""";

        File.WriteAllText(path, html, new UTF8Encoding(false));
    }
}