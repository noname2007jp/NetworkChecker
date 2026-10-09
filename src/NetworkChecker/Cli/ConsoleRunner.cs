// File: src/NetworkChecker/Cli/ConsoleRunner.cs
using NetworkChecker.Core;
using NetworkChecker.Diagnostics;
using NetworkChecker.Reporting;

namespace NetworkChecker.Cli;

/// <summary>サポート担当者向けコンソールモード。</summary>
public static class ConsoleRunner
{
    public static async Task<int> RunAsync(
        AppConfig config, IReadOnlyList<TargetDefinition> targets, CliOptions options)
    {
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }
        if (options.ShowVersion)
        {
            Console.WriteLine($"NetworkChecker {VersionInfo.Version}");
            return 0;
        }
        if (options.HasUnknown)
        {
            Console.WriteLine(Strings.Current == "en"
                ? "Note: some options were not recognized. Use --help to list options."
                : "注意: 認識できないオプションがありました。--help で一覧を確認できます。");
            Console.WriteLine();
        }

        Console.WriteLine($" {Strings.Get("AppTitle")} v{VersionInfo.Version}    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine(new string('=', 72));

        using var logger = new Logger(config, enableFile: !options.NoLog);
        var engine = new DiagnosticEngine(config, targets, logger);
        engine.CheckCompleted += (_, r) => PrintResult(r, options.Verbose);

        DiagnosticReport report;
        try
        {
            report = await engine.RunAsync(options.CheckFilter, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 5;
        }

        Console.WriteLine(new string('=', 72));
        WriteColored(
            $" {Strings.Get("CliOverall")}: {report.VerdictMessage} ({report.VerdictCode})",
            report.ExitCode switch
            {
                0 => ConsoleColor.Green,
                1 => ConsoleColor.Yellow,
                _ => ConsoleColor.Red,
            });

        if (report.SuspectedCauses.Count > 0)
        {
            Console.WriteLine($" {Strings.Get("CausesTitle")}:");
            foreach (var c in report.SuspectedCauses) Console.WriteLine($"   - {c}");
        }
        if (report.Hints.Count > 0)
        {
            Console.WriteLine($" {Strings.Get("HintsTitle")}:");
            foreach (var h in report.Hints) Console.WriteLine($"   - {h}");
        }
        Console.WriteLine($" {Strings.Get("Disclaimer")}");
        Console.WriteLine();

        SaveOutputs(config, options, report, logger);

        Console.WriteLine($" {Strings.Get("CliExitCode")}: {report.ExitCode}");
        return report.ExitCode;
    }

    private static void SaveOutputs(
        AppConfig config, CliOptions options, DiagnosticReport report, Logger logger)
    {
        var jsonPath = options.OutputJsonPath ?? AppPaths.ResultJsonPath;
        try
        {
            JsonReporter.Save(report, jsonPath);
            Console.WriteLine($" result.json : {Path.GetFullPath(jsonPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [WARN] result.json: {ex.Message}");
        }

        if (config.GenerateHtmlReport && !options.NoReport)
        {
            var htmlPath = options.OutputHtmlPath ?? AppPaths.ReportHtmlPath;
            try
            {
                HtmlReporter.Save(report, logger.Entries, htmlPath, config.ReportRefreshSeconds);
                Console.WriteLine($" report.html : {Path.GetFullPath(htmlPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [WARN] report.html: {ex.Message}");
            }
        }

        if (logger.FilePath is not null)
        {
            Console.WriteLine($" log         : {logger.FilePath}");
        }
    }

    private static void PrintResult(CheckResult r, bool verbose)
    {
        var (tag, color) = r.Status switch
        {
            CheckStatus.Ok => (" OK  ", ConsoleColor.Green),
            CheckStatus.Warning => ("WARN ", ConsoleColor.Yellow),
            CheckStatus.Failed => ("FAIL ", ConsoleColor.Red),
            CheckStatus.Skipped => ("SKIP ", ConsoleColor.Blue),
            _ => (" --  ", ConsoleColor.Gray),
        };

        Console.Write(" [");
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(tag);
        Console.ForegroundColor = prev;
        Console.Write($"] {r.Id}  {r.Name}");

        if (!string.IsNullOrEmpty(r.Detail)) Console.Write($"  {r.Detail}");
        if (r.LatencyMs is not null && r.Status != CheckStatus.Skipped) Console.Write($"  {r.LatencyMs} ms");
        if (r.Attempts > 1) Console.Write($"  ({Strings.Format("CliAttempts", r.Attempts)})");
        Console.WriteLine();

        if (verbose)
        {
            foreach (var e in r.Evidence) Console.WriteLine($"         | {e}");
        }
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
通信状態チェッカー / Network Checker

使い方 / Usage:
  NetworkChecker.exe                   GUI で起動 / Run GUI (double-click)
  NetworkChecker.exe --gui             明示的に GUI を起動 / Force GUI
  NetworkChecker.exe --verbose         詳細表示 / Verbose output
  NetworkChecker.exe --checks dns      DNS 系のみ / DNS checks only
  NetworkChecker.exe --checks net-004,dns-003
  NetworkChecker.exe --output-json result.json
  NetworkChecker.exe --output-html report.html
  NetworkChecker.exe --lang ja|en
  NetworkChecker.exe --no-report --no-log
  NetworkChecker.exe --version

終了コード / Exit codes:
  0  正常 / OK
  1  警告または一部失敗 / Warning or partial failure
  2  通信障害の可能性 / Connectivity problem suspected
  3  DNS障害の可能性 / DNS problem suspected
  4  設定エラー / Configuration error
  5  実行環境エラー / Runtime error
""");
    }
}