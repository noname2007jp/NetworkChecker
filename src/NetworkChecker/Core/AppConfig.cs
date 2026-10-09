// File: src/NetworkChecker/Core/AppConfig.cs
namespace NetworkChecker.Core;

public sealed class AppConfig
{
    public int TimeoutMs { get; set; } = 5000;
    public int RetryCount { get; set; } = 2;
    public int RetryIntervalMs { get; set; } = 500;
    public bool EnableIpv6 { get; set; } = true;
    public bool EnableHttpTest { get; set; } = true;
    public bool EnableCertificateTest { get; set; } = true;
    public bool SaveLogs { get; set; } = true;
    public bool GenerateHtmlReport { get; set; } = true;
    public int ReportRefreshSeconds { get; set; } = 300;
    public bool PrivacyMode { get; set; } = true;
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>"ja" / "en" / null（OS の表示言語から自動判定）</summary>
    public string? Language { get; set; }

    /// <summary>DNS 診断に使用する標準テストドメイン。</summary>
    public string TestDomain { get; set; } = "www.example.com";

    /// <summary>比較用 DNS（Google Public DNS / Cloudflare DNS）。</summary>
    public string[] ReferenceDnsServers { get; set; } = { "8.8.8.8", "8.8.4.4", "1.1.1.1", "1.0.0.1" };

    /// <summary>DNS を使わない外部到達性の確認先（既知 IP）。</summary>
    public string[] ExternalPingTargets { get; set; } = { "1.1.1.1", "8.8.8.8" };

    /// <summary>時刻確認に使用する NTP サーバー。</summary>
    public string[] NtpServers { get; set; } = { "time.cloudflare.com", "time.windows.com" };

    /// <summary>WAN-002 の基準 HTTPS URL。</summary>
    public string HttpsBaselineUrl { get; set; } = "https://example.com/";
}

public sealed class TargetDefinition
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public int[] ExpectedStatus { get; set; } = { 200, 301, 302 };
    public int TimeoutMs { get; set; } = 5000;
    public bool Enabled { get; set; } = true;
}