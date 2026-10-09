// File: src/NetworkChecker/Core/Strings.cs
namespace NetworkChecker.Core;

/// <summary>日本語 / 英語の表示文字列。</summary>
public static class Strings
{
    public static string Current { get; private set; } = "ja";

    public static void Initialize(string? language)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            Current = language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ja";
            return;
        }
        Current = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ja" : "en";
    }

    private static readonly Dictionary<string, (string Ja, string En)> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AppTitle"] = ("通信状態チェッカー", "Network Checker"),
        ["BtnStart"] = ("診断開始", "Start"),
        ["BtnRunning"] = ("診断中…", "Running…"),
        ["BtnSettings"] = ("詳細設定", "Settings"),
        ["BtnSave"] = ("結果保存", "Save results"),
        ["BtnOk"] = ("保存", "Save"),
        ["BtnCancel"] = ("キャンセル", "Cancel"),
        ["VerdictIdle"] = ("「診断開始」ボタンを押してください", "Press \"Start\" to run diagnostics"),
        ["VerdictRunning"] = ("診断中…（{0} 項目完了）", "Running… ({0} checks done)"),
        ["ProgressFormat"] = ("{0} / {1} 項目完了", "{0} / {1} checks done"),
        ["ColStatus"] = ("状態", "Status"),
        ["ColId"] = ("ID", "ID"),
        ["ColName"] = ("項目", "Check"),
        ["ColDetail"] = ("詳細", "Detail"),
        ["ColLatency"] = ("応答時間", "Latency"),
        ["CausesTitle"] = ("推定原因", "Suspected causes"),
        ["HintsTitle"] = ("対処のヒント", "Hints"),
        ["Disclaimer"] = ("※ この結果だけで、サービス提供者側の障害とは断定できません。", "* This result alone does not prove a provider-side outage."),
        ["MaskNote"] = ("プライバシー情報はマスクされています", "Privacy information is masked"),
        ["SavedTo"] = ("保存しました：{0}", "Saved: {0}"),
        ["CliOverall"] = ("総合判定", "Overall"),
        ["CliExitCode"] = ("終了コード", "Exit code"),
        ["CliAttempts"] = ("{0} 回試行", "{0} attempts"),

        ["check.NET-001"] = ("ネットワーク接続", "Network adapter"),
        ["check.NET-002"] = ("IPアドレス取得", "IP address"),
        ["check.NET-003"] = ("デフォルトゲートウェイ", "Default gateway"),
        ["check.NET-004"] = ("ルーター接続", "Router connection"),
        ["check.DNS-001"] = ("使用DNS取得", "DNS servers in use"),
        ["check.DNS-002"] = ("DNSサーバー応答", "DNS server response"),
        ["check.DNS-003"] = ("DNS名前解決", "DNS name resolution"),
        ["check.DNS-004"] = ("比較用DNS", "Reference DNS"),
        ["check.WAN-001"] = ("外部IP接続", "External IP reachability"),
        ["check.WAN-002"] = ("HTTPS接続", "HTTPS connection"),
        ["check.APP-001"] = ("指定サービス接続", "Service check"),
        ["check.SYS-001"] = ("時刻確認", "System time"),
        ["check.SYS-002"] = ("プロキシ確認", "Proxy settings"),

        ["verdict.HEALTHY"] = ("インターネット接続は正常です", "Internet connection appears healthy"),
        ["verdict.HEALTHY_WITH_WARNINGS"] = ("概ね正常ですが、注意点があります", "Mostly healthy, with warnings"),
        ["verdict.ADAPTER_DOWN"] = ("ネットワークアダプターに問題がある可能性があります", "The network adapter may have a problem"),
        ["verdict.NO_IP_ADDRESS"] = ("IPアドレスを取得できていない可能性があります", "No IP address may be assigned"),
        ["verdict.NO_GATEWAY"] = ("ルーター（ゲートウェイ）が設定されていない可能性があります", "A default gateway may not be configured"),
        ["verdict.GATEWAY_UNREACHABLE"] = ("ルーターに接続できていない可能性があります", "The router may be unreachable"),
        ["verdict.NO_DNS_CONFIGURED"] = ("DNSサーバーが設定されていない可能性があります", "No DNS server may be configured"),
        ["verdict.DNS_SUSPECTED"] = ("DNSに問題がある可能性があります", "A DNS problem is suspected"),
        ["verdict.DNS_FILTERED"] = ("DNS通信が遮断されている可能性があります", "DNS traffic may be blocked"),
        ["verdict.WAN_DOWN"] = ("インターネット回線に問題がある可能性があります", "An internet line problem is suspected"),
        ["verdict.HTTPS_SUSPECTED"] = ("HTTPS通信に問題がある可能性があります", "An HTTPS problem is suspected"),
        ["verdict.SERVICE_DEGRADED"] = ("特定のサービスに問題がある可能性があります", "A specific service may have a problem"),
        ["verdict.UNKNOWN"] = ("判定できませんでした", "Could not determine"),

        ["cause.ADAPTER_DOWN"] = ("Wi-Fi がオフ／機内モード／アダプター無効／ドライバーの問題", "Wi-Fi off, airplane mode, disabled adapter, or driver issue"),
        ["cause.NO_IP"] = ("DHCP からアドレスを取得できていません", "Could not obtain an address via DHCP"),
        ["cause.NO_GATEWAY"] = ("ルーターの停止または設定の問題", "Router down or misconfiguration"),
        ["cause.GATEWAY"] = ("ルーターの停止／ケーブル／Wi-Fi 電波の問題", "Router down, cable, or Wi-Fi signal issue"),
        ["cause.NO_DNS"] = ("DHCP の設定またはルーターの問題", "DHCP configuration or router issue"),
        ["cause.DNS_SUSPECTED"] = ("ルーターの DNS 中継またはプロバイダ DNS サーバーの問題", "Router DNS relay or ISP DNS server issue"),
        ["cause.DNS_FILTERED"] = ("ファイアウォール／セキュリティソフト／VPN による DNS(53番) の遮断", "Firewall, security software, or VPN blocking DNS (port 53)"),
        ["cause.WAN"] = ("プロバイダ障害／ONU・モデムの異常／回線工事", "ISP outage, ONU/modem failure, or line maintenance"),
        ["cause.HTTPS"] = ("TLS・証明書／プロキシ／ファイアウォールの問題", "TLS/certificate, proxy, or firewall issue"),
        ["cause.SERVICE"] = ("サービス提供者側の障害またはメンテナンス", "Provider-side outage or maintenance"),
        ["cause.CLOCK"] = ("時刻のずれが TLS 証明書の検証に影響している可能性", "Clock skew may be breaking TLS certificate validation"),
        ["cause.PROXY"] = ("プロキシ設定が影響している可能性", "Proxy settings may be interfering"),

        ["hint.REBOOT_ROUTER"] = ("ルーター（と ONU/モデム）を再起動してください", "Restart the router (and ONU/modem)"),
        ["hint.CHECK_WIFI"] = ("Wi-Fi の接続先とパスワードを確認してください", "Check the Wi-Fi network and password"),
        ["hint.WAIT_ISP"] = ("時間をおいて再試行し、改善しない場合はプロバイダの障害情報を確認してください", "Retry later; check ISP outage information if it persists"),
        ["hint.CHECK_SECURITY"] = ("セキュリティソフトや VPN を一時的に無効にして再試行してください", "Temporarily disable security software or VPN and retry"),
        ["hint.CHECK_TIME"] = ("日付と時刻の自動設定を有効にしてください", "Enable automatic date and time"),
        ["hint.CHECK_SERVICE"] = ("サービスの障害情報・ステータスページを確認してください", "Check the service status page"),
        ["hint.DNS_CHANGE"] = ("DNS 設定の変更は管理者に相談してください（本ツールは設定を変更しません）", "Consult an administrator before changing DNS (this tool never changes settings)"),

        ["err.timeout"] = ("応答がありません（タイムアウト）", "No response (timeout)"),
        ["err.refused"] = ("宛先には到達しましたが接続を拒否されました", "Reached the host but the connection was refused"),
        ["err.nameResolution"] = ("ドメイン名をIPアドレスに変換できません", "Could not resolve the domain name"),
        ["err.tls"] = ("HTTPS認証または証明書を確認できません", "TLS authentication or certificate problem"),
        ["err.noResponse"] = ("DNSサーバーから応答がありません", "No response from the DNS server"),
        ["err.permission"] = ("一部の詳細情報を取得できません", "Some detailed information is unavailable"),

        ["detail.noAdapter"] = ("有効なネットワークアダプターが見つかりません", "No active network adapter found"),
        ["detail.noIp"] = ("IPアドレスが割り当てられていません", "No IP address assigned"),
        ["detail.noGateway"] = ("デフォルトゲートウェイが設定されていません", "No default gateway configured"),
        ["detail.noDns"] = ("DNSサーバーが設定されていません", "No DNS server configured"),
        ["detail.servicesOk"] = ("{0} 件すべて正常", "All {0} target(s) OK"),
        ["detail.servicesPartial"] = ("{0} / {1} 件が正常", "{0} / {1} target(s) OK"),
        ["detail.servicesAllFailed"] = ("すべての対象サービスが応答しません", "All target services failed"),
        ["detail.timeOk"] = ("時刻ずれ {0} 秒", "Clock skew {0} s"),
        ["detail.timeSkew"] = ("時刻が約 {0} 秒ずれています", "Clock is off by about {0} s"),
        ["detail.timeUnknown"] = ("基準時刻を取得できませんでした", "Could not obtain a reference time"),
        ["detail.proxyOn"] = ("プロキシ有効：{0}", "Proxy enabled: {0}"),
        ["detail.proxyPac"] = ("自動構成スクリプト（PAC）が設定されています", "An auto-config script (PAC) is set"),
        ["detail.proxyOff"] = ("プロキシは使用されていません", "No proxy configured"),

        ["skip.dnsFailed"] = ("DNS失敗のため省略", "Skipped (DNS failed)"),
        ["skip.dependency"] = ("前提チェック未実施のため省略", "Skipped (prerequisite not met)"),
        ["skip.disabled"] = ("設定で無効化されています", "Disabled in settings"),
        ["skip.noTargets"] = ("対象サービスが設定されていません", "No service targets configured"),

        ["evidence.serverResponded"] = ("サーバーから応答があります（ステータスは想定外）", "The server responded (unexpected status)"),
        ["evidence.httpErrorButResponded"] = ("HTTPエラーですがサーバーは応答しています", "HTTP error, but the server responded"),
        ["evidence.dnsMismatch"] = ("使用DNSと比較用DNSで応答内容が異なります", "System DNS and reference DNS returned different answers"),
        ["evidence.certExpiring"] = ("証明書の有効期限が近づいています（残り {0} 日）", "Certificate expires soon ({0} days left)"),
        ["evidence.certHostname"] = ("証明書のホスト名が一致しません", "Certificate hostname mismatch"),

        ["adapter.ethernet"] = ("有線", "Ethernet"),

        ["set.timeout"] = ("タイムアウト (ms)", "Timeout (ms)"),
        ["set.retry"] = ("再試行回数", "Retry count"),
        ["set.retryInterval"] = ("再試行間隔 (ms)", "Retry interval (ms)"),
        ["set.refresh"] = ("レポート自動更新 (秒、0=無効)", "Report auto-refresh (s, 0=off)"),
        ["set.retention"] = ("ログ保存日数", "Log retention (days)"),
        ["set.lang"] = ("言語 / Language", "Language"),
        ["set.langAuto"] = ("自動", "Auto"),
        ["set.ipv6"] = ("IPv6 を有効化", "Enable IPv6"),
        ["set.http"] = ("HTTP/HTTPS テストを有効化", "Enable HTTP/HTTPS tests"),
        ["set.cert"] = ("証明書チェックを有効化", "Enable certificate checks"),
        ["set.logs"] = ("ログを保存する", "Save logs"),
        ["set.report"] = ("HTML レポートを生成する", "Generate HTML report"),
        ["set.privacy"] = ("プライバシー情報をマスクする", "Mask privacy information"),
        ["set.testDomain"] = ("テストドメイン", "Test domain"),
        ["set.baseline"] = ("基準 HTTPS URL", "Baseline HTTPS URL"),
        ["set.refDns"] = ("比較用 DNS（カンマ区切り）", "Reference DNS (comma separated)"),
        ["set.pingTargets"] = ("外部到達確認 IP（カンマ区切り）", "External ping targets (comma separated)"),
        ["set.ntp"] = ("NTP サーバー（カンマ区切り）", "NTP servers (comma separated)"),
        ["set.targets"] = ("診断対象サービス（targets.json）", "Service targets (targets.json)"),
        ["set.colEnabled"] = ("有効", "On"),
        ["set.colName"] = ("名前", "Name"),
        ["set.colExpected"] = ("期待ステータス", "Expected status"),
        ["set.colTimeout"] = ("タイムアウト", "Timeout"),
    };

    public static string Get(string key)
        => Table.TryGetValue(key, out var v) ? (Current == "en" ? v.En : v.Ja) : key;

    public static string Format(string key, params object[] args)
        => string.Format(Get(key), args);
}