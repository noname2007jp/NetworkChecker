// File: src/NetworkChecker/Program.cs
using System.Runtime.InteropServices;
using System.Text;
using NetworkChecker.Cli;
using NetworkChecker.Core;
using NetworkChecker.Gui;

namespace NetworkChecker;

internal static class Program
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [STAThread]
    private static int Main(string[] args)
    {
        AppConfig config;
        List<TargetDefinition> targets;
        try
        {
            (config, targets) = ConfigLoader.LoadOrCreate();
        }
        catch (ConfigException ex)
        {
            ShowFatal($"設定ファイルを読み込めません。\n{ex.Message}");
            return 4;
        }

        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            ShowFatal(ex.Message);
            return 4;
        }

        Strings.Initialize(options.Lang ?? config.Language);
        PrivacyMask.Enabled = config.PrivacyMode;

        bool attached = AttachConsole(AttachParentProcess);
        bool gui = options.ForceGui || (args.Length == 0 && !attached);

        if (gui)
        {
            if (attached) FreeConsole();
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(config, targets));
            return 0;
        }

        bool selfAllocated = false;
        if (!attached)
        {
            AllocConsole();
            selfAllocated = true;
        }
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        int code;
        try
        {
            code = ConsoleRunner.RunAsync(config, targets, options).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            code = 5;
        }

        Console.Out.Flush();
        if (selfAllocated)
        {
            Console.WriteLine();
            Console.WriteLine(Strings.Current == "en" ? "(Press Enter to close)" : "（Enter キーで閉じます）");
            Console.ReadLine();
        }
        return code;
    }

    private static void ShowFatal(string message)
    {
        if (AttachConsole(AttachParentProcess))
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.Error.WriteLine(message);
        }
        else
        {
            MessageBox.Show(message, "通信状態チェッカー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
