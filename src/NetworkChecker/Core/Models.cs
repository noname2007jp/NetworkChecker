// File: src/NetworkChecker/Core/Models.cs
using System.Text.Json.Serialization;

namespace NetworkChecker.Core;

public enum CheckStatus
{
    Ok,
    Warning,
    Failed,
    Unknown,
    Skipped,
}

public sealed class CheckResult
{
    public required string Id { get; set; }
    public string Name { get; set; } = "";
    public CheckStatus Status { get; set; } = CheckStatus.Unknown;
    public string Detail { get; set; } = "";
    public long? LatencyMs { get; set; }
    public int Attempts { get; set; } = 1;
    public List<string> Evidence { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

public sealed class EnvironmentInfo
{
    public string OsVersion { get; set; } = "";
    public string? ComputerName { get; set; }
    public string? UserName { get; set; }

    [JsonIgnore]
    public string? ComputerNameFull { get; set; }

    [JsonIgnore]
    public string? UserNameFull { get; set; }

    public string? AdapterType { get; set; }
    public string? AdapterName { get; set; }
    public bool Ipv6Enabled { get; set; }
    public string? Proxy { get; set; }
}

public sealed class DiagnosticReport
{
    public string Tool { get; set; } = "NetworkChecker";
    public string ToolVersion { get; set; } = "";
    public string Language { get; set; } = "ja";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public string VerdictCode { get; set; } = "UNKNOWN";
    public string VerdictMessage { get; set; } = "";
    public int ExitCode { get; set; }
    public List<string> SuspectedCauses { get; set; } = new();
    public List<string> Hints { get; set; } = new();
    public List<CheckResult> Checks { get; set; } = new();
    public EnvironmentInfo Environment { get; set; } = new();
}