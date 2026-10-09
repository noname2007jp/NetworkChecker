// File: src/NetworkChecker/Core/AppPaths.cs
namespace NetworkChecker.Core;

/// <summary>
/// 設定・ログ・レポートの保存先。
/// EXE と同じフォルダに書き込めない場合は %LOCALAPPDATA%\NetworkChecker を使用する。
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> BaseDirLazy = new(ResolveBaseDir);

    public static string BaseDir => BaseDirLazy.Value;
    public static string LogDir => Path.Combine(BaseDir, "logs");
    public static string ConfigPath => Path.Combine(BaseDir, "config.json");
    public static string TargetsPath => Path.Combine(BaseDir, "targets.json");
    public static string ResultJsonPath => Path.Combine(BaseDir, "result.json");
    public static string ReportHtmlPath => Path.Combine(BaseDir, "report.html");

    private static string ResolveBaseDir()
    {
        var exeDir = AppContext.BaseDirectory;
        if (CanWrite(exeDir)) return exeDir;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetworkChecker");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            var test = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(test, "");
            File.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }
}