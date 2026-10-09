// File: src/NetworkChecker/Diagnostics/DiagnosticEngine.cs
using NetworkChecker.Core;

namespace NetworkChecker.Diagnostics;

/// <summary>診断の実行・依存制御・総合判定を行うエンジン。</summary>
public sealed class DiagnosticEngine
{
    private sealed record CheckDefinition(
        string Id,
        Func<DiagnosticContext, CancellationToken, Task<CheckResult>> Run,
        string[] Depends);

    // 仕様書 3.1 の検査順序
    private static readonly CheckDefinition[] Definitions =
    {
        new("NET-001", Checks.Net001AdaptersAsync, Array.Empty<string>()),
        new("NET-002", Checks.Net002IpAsync, new[] { "NET-001" }),
        new("NET-003", Checks.Net003GatewayAsync, new[] { "NET-001" }),
        new("NET-004", Checks.Net004GatewayPingAsync, new[] { "NET-003" }),
        new("DNS-001", Checks.Dns001ServersAsync, new[] { "NET-001" }),
        new("DNS-002", Checks.Dns002ServerPingAsync, new[] { "DNS-001" }),
        new("DNS-003", Checks.Dns003ResolutionAsync, new[] { "DNS-001" }),
        new("DNS-004", Checks.Dns004CompareAsync, Array.Empty<string>()),
        new("WAN-001", Checks.Wan001ExternalIpAsync, Array.Empty<string>()),
        new("WAN-002", Checks.Wan002HttpsAsync, new[] { "DNS-001", "DNS-003" }),
        new("APP-001", Checks.App001ServicesAsync, new[] { "DNS-001", "DNS-003" }),
        new("SYS-001", Checks.Sys001TimeAsync, Array.Empty<string>()),
        new("SYS-002", Checks.Sys002ProxyAsync, Array.Empty<string>()),
    };

    private readonly AppConfig _config;
    private readonly IReadOnlyList<TargetDefinition> _targets;
    private readonly Logger _logger;
    private readonly DiagnosticContext _ctx;

    public event EventHandler<string>? CheckStarted;
    public event EventHandler<CheckResult>? CheckCompleted;

    public DiagnosticEngine(AppConfig config, IReadOnlyList<TargetDefinition> targets, Logger logger)
    {
        _config = config;
        _targets = targets;
        _logger = logger;
        _ctx = new DiagnosticContext { Config = config, Targets = targets, Log = logger };
    }

    public async Task<DiagnosticReport> RunAsync(IReadOnlySet<string>? filter, CancellationToken ct)
    {
        PrivacyMask.Reset();

        var report = new DiagnosticReport
        {
            ToolVersion = VersionInfo.Version,
            Language = Strings.Current,
            StartedAt = DateTimeOffset.Now,
        };
        _logger.Info("Diagnostic started");

        foreach (var def in BuildPlan(filter))
        {
            ct.ThrowIfCancellationRequested();
            CheckStarted?.Invoke(this, def.Id);

            CheckResult result;
            try
            {
                result = await def.Run(_ctx, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = new CheckResult { Id = def.Id, Status = CheckStatus.Unknown, Detail = ex.Message };
            }

            result.Id = def.Id;
            result.Name = Strings.Get($"check.{def.Id}");
            report.Checks.Add(result);

            _logger.Write(
                result.Status is CheckStatus.Failed or CheckStatus.Warning ? "WARN" : "INFO",
                $"{result.Id} {result.Name}: {result.Status.ToString().ToUpperInvariant()} {result.Detail}");
            foreach (var e in result.Evidence)
            {
                _logger.Info($"  {result.Id} | {e}");
            }

            CheckCompleted?.Invoke(this, result);
        }

        report.Environment = CollectEnvironment();
        VerdictCalculator.Evaluate(report, _ctx);
        report.FinishedAt = DateTimeOffset.Now;
        _logger.Info($"Overall result: {report.VerdictCode}");
        return report;
    }

    /// <summary>--checks フィルタ。カテゴリ（DNS 等）または ID を指定可能。依存チェックも自動で含める。</summary>
    private static List<CheckDefinition> BuildPlan(IReadOnlySet<string>? filter)
    {
        if (filter is null || filter.Count == 0) return Definitions.ToList();

        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filter)
        {
            foreach (var def in Definitions)
            {
                if (def.Id.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                    def.Id.StartsWith(f + "-", StringComparison.OrdinalIgnoreCase))
                {
                    AddWithDependencies(def, wanted);
                }
            }
        }
        return Definitions.Where(d => wanted.Contains(d.Id)).ToList();
    }

    private static void AddWithDependencies(CheckDefinition def, HashSet<string> wanted)
    {
        if (!wanted.Add(def.Id)) return;
        foreach (var dep in def.Depends)
        {
            var parent = Definitions.FirstOrDefault(d => d.Id == dep);
            if (parent is not null) AddWithDependencies(parent, wanted);
        }
    }

    private EnvironmentInfo CollectEnvironment()
        => new()
        {
            OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ComputerNameFull = Environment.MachineName,
            UserNameFull = Environment.UserName,
            ComputerName = PrivacyMask.Name(Environment.MachineName),
            UserName = PrivacyMask.Name(Environment.UserName),
            AdapterType = _ctx.AdapterTypeName,
            AdapterName = _ctx.AdapterName,
            Ipv6Enabled = _config.EnableIpv6,
            Proxy = _ctx.ProxyEnabled ? (_ctx.ProxyDetail ?? "enabled") : null,
        };
}