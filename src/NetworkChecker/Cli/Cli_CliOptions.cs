// File: src/NetworkChecker/Cli/CliOptions.cs
namespace NetworkChecker.Cli;

/// <summary>
/// コマンドライン引数。--verbose / -Verbose / /verbose 形式をすべて受け付ける。
/// </summary>
public sealed class CliOptions
{
    public bool ForceGui { get; private set; }
    public bool Verbose { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool ShowVersion { get; private set; }
    public bool NoReport { get; private set; }
    public bool NoLog { get; private set; }
    public bool HasUnknown { get; private set; }
    public string? OutputJsonPath { get; private set; }
    public string? OutputHtmlPath { get; private set; }
    public string? Lang { get; private set; }
    public HashSet<string>? CheckFilter { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var raw = args[i];

            // --checks=dns 形式の分解
            string? inlineValue = null;
            var eq = raw.IndexOf('=');
            if (eq > 0)
            {
                inlineValue = raw[(eq + 1)..];
                raw = raw[..eq];
            }

            var name = raw.TrimStart('-', '/').ToLowerInvariant();

            string Value()
            {
                if (inlineValue is not null) return inlineValue;
                if (i + 1 >= args.Length) throw new ArgumentException($"option requires a value: {raw}");
                return args[++i];
            }

            switch (name)
            {
                case "gui": options.ForceGui = true; break;
                case "verbose": case "v": options.Verbose = true; break;
                case "help": case "h": case "?": options.ShowHelp = true; break;
                case "version": options.ShowVersion = true; break;
                case "noreport": options.NoReport = true; break;
                case "nolog": options.NoLog = true; break;
                case "checks":
                    options.CheckFilter = Value()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => s.ToUpperInvariant())
                        .ToHashSet();
                    break;
                case "outputjson": options.OutputJsonPath = Value(); break;
                case "outputhtml": options.OutputHtmlPath = Value(); break;
                case "lang": options.Lang = Value().ToLowerInvariant(); break;
                default: options.HasUnknown = true; break;
            }
        }

        return options;
    }
}