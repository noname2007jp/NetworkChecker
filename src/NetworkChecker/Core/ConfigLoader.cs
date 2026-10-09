// File: src/NetworkChecker/Core/ConfigLoader.cs
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetworkChecker.Core;

public sealed class ConfigException : Exception
{
    public ConfigException(string message, Exception? inner = null) : base(message, inner) { }
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// EXE 内蔵のデフォルトを初回起動時に展開し、外部ファイルを読み込む。
    /// 外部ファイルが存在すればそちらが優先される。
    /// </summary>
    public static (AppConfig Config, List<TargetDefinition> Targets) LoadOrCreate()
    {
        try
        {
            EnsureExtracted(AppPaths.ConfigPath, "NetworkChecker.config.json");
            EnsureExtracted(AppPaths.TargetsPath, "NetworkChecker.targets.json");
        }
        catch (ConfigException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConfigException($"設定ファイルの初期展開に失敗しました: {ex.Message}", ex);
        }

        AppConfig config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigPath), ReadOptions)
                     ?? throw new ConfigException("config.json の内容が空です");
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"config.json の形式が不正です: {ex.Message}", ex);
        }

        List<TargetDefinition> targets;
        try
        {
            targets = JsonSerializer.Deserialize<List<TargetDefinition>>(File.ReadAllText(AppPaths.TargetsPath), ReadOptions)
                      ?? new List<TargetDefinition>();
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"targets.json の形式が不正です: {ex.Message}", ex);
        }

        return (config, targets);
    }

    public static void SaveConfig(AppConfig config)
        => File.WriteAllText(AppPaths.ConfigPath, JsonSerializer.Serialize(config, WriteOptions));

    public static void SaveTargets(IReadOnlyList<TargetDefinition> targets)
        => File.WriteAllText(AppPaths.TargetsPath, JsonSerializer.Serialize(targets, WriteOptions));

    private static void EnsureExtracted(string path, string resourceName)
    {
        if (File.Exists(path)) return;

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ConfigException($"内蔵設定 {resourceName} が見つかりません");
        using var file = File.Create(path);
        stream.CopyTo(file);
    }
}