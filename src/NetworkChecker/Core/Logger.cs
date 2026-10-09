// File: src/NetworkChecker/Core/Logger.cs
using System.Text;

namespace NetworkChecker.Core;

/// <summary>
/// logs/checker-yyyyMMdd.log への追記とメモリ保持を行うロガー。
/// 古いログは logRetentionDays に従って削除する。
/// </summary>
public sealed class Logger : IDisposable
{
    private readonly StreamWriter? _writer;
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;
    public string? FilePath { get; }

    public Logger(AppConfig config, bool enableFile)
    {
        if (!enableFile || !config.SaveLogs) return;

        try
        {
            Directory.CreateDirectory(AppPaths.LogDir);
            FilePath = Path.Combine(AppPaths.LogDir, $"checker-{DateTime.Now:yyyyMMdd}.log");
            _writer = new StreamWriter(FilePath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
            CleanupOldLogs(config.LogRetentionDays);
        }
        catch
        {
            _writer = null;
            FilePath = null;
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:sszzz} [{level}] {message}";
        lock (_entries)
        {
            _entries.Add(line);
            _writer?.WriteLine(line);
        }
    }

    private void CleanupOldLogs(int retentionDays)
    {
        if (retentionDays <= 0) return;

        try
        {
            var threshold = DateTime.Now.AddDays(-retentionDays);
            foreach (var file in Directory.GetFiles(AppPaths.LogDir, "checker-*.log"))
            {
                if (File.GetLastWriteTime(file) < threshold) File.Delete(file);
            }
        }
        catch
        {
            // ログ削除失敗は診断に影響させない
        }
    }

    public void Dispose() => _writer?.Dispose();
}