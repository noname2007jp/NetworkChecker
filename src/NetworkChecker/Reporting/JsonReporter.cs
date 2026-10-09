// File: src/NetworkChecker/Reporting/JsonReporter.cs
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkChecker.Core;

namespace NetworkChecker.Reporting;

public static class JsonReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(DiagnosticReport report, string path)
    {
        var json = JsonSerializer.Serialize(report, Options);
        File.WriteAllText(path, json);
    }
}