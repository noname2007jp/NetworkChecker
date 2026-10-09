// File: src/NetworkChecker/Diagnostics/DiagnosticContext.cs
using System.Net;
using System.Net.NetworkInformation;
using NetworkChecker.Core;

namespace NetworkChecker.Diagnostics;

/// <summary>チェック間で共有する診断コンテキスト。</summary>
public sealed class DiagnosticContext
{
    public required AppConfig Config { get; init; }
    public required IReadOnlyList<TargetDefinition> Targets { get; init; }
    public required Logger Log { get; init; }

    public NetworkInterface? PrimaryAdapter { get; set; }
    public List<NetworkInterface> ActiveAdapters { get; } = new();
    public List<IPAddress> LocalIPv4 { get; } = new();
    public List<IPAddress> LocalIPv6 { get; } = new();
    public List<IPAddress> Gateways { get; } = new();
    public List<IPAddress> SystemDns { get; } = new();
    public List<IPAddress> ResolvedBySystemDns { get; } = new();

    public string? AdapterTypeName { get; set; }
    public string? AdapterName { get; set; }

    public bool GatewayOk { get; set; }
    public bool SystemDnsResponsive { get; set; }
    public bool ReferenceDnsResponsive { get; set; }
    public bool DnsResolutionAttempted { get; set; }
    public bool NameResolutionOk { get; set; }
    public bool ExternalIpOk { get; set; }
    public bool HttpsBaselineOk { get; set; }
    public int ServiceOkCount { get; set; }
    public int ServiceFailCount { get; set; }

    public DateTimeOffset? ReferenceTime { get; set; }
    public double? ClockSkewSeconds { get; set; }
    public bool TimeWarning { get; set; }
    public bool ProxyEnabled { get; set; }
    public string? ProxyDetail { get; set; }

    public HttpProbeResult? BaselineHttps { get; set; }
}