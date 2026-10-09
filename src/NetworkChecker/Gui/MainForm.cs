// File: src/NetworkChecker/Gui/MainForm.cs
using NetworkChecker.Core;
using NetworkChecker.Diagnostics;
using NetworkChecker.Reporting;

namespace NetworkChecker.Gui;

/// <summary>一般利用者向けメイン画面。</summary>
public sealed class MainForm : Form
{
    private static readonly Color AccentBlue = Color.FromArgb(9, 105, 218);
    private static readonly Color ColorOk = Color.FromArgb(26, 127, 55);
    private static readonly Color ColorOkBg = Color.FromArgb(218, 251, 225);
    private static readonly Color ColorWarn = Color.FromArgb(154, 103, 0);
    private static readonly Color ColorWarnBg = Color.FromArgb(255, 248, 197);
    private static readonly Color ColorFail = Color.FromArgb(207, 34, 46);
    private static readonly Color ColorFailBg = Color.FromArgb(255, 235, 233);
    private static readonly Color ColorIdleBg = Color.FromArgb(234, 238, 242);
    private static readonly Color ColorSkip = Color.FromArgb(9, 105, 218);
    private static readonly Color ColorUnknown = Color.FromArgb(110, 119, 129);
    private static readonly Color ColorNote = Color.FromArgb(87, 96, 106);

    private readonly AppConfig _config;
    private readonly List<TargetDefinition> _targets;

    private readonly Button _btnStart;
    private readonly Button _btnSettings;
    private readonly Button _btnSave;
    private readonly Panel _verdictPanel;
    private readonly Label _verdictLabel;
    private readonly Label _verdictSubLabel;
    private readonly ListView _list;
    private readonly ProgressBar _progress;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;

    private DiagnosticReport? _lastReport;
    private int _totalChecks;
    private int _doneChecks;

    public MainForm(AppConfig config, List<TargetDefinition> targets)
    {
        _config = config;
        _targets = targets;

        Text = $"{Strings.Get("AppTitle")}  v{VersionInfo.Version}";
        ClientSize = new Size(800, 660);
        MinimumSize = new Size(700, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // ボタン
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));   // 総合判定バナー
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // チェック一覧
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // プログレス
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // 原因・ヒント
        Controls.Add(root);

        // ---- ボタン
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        _btnStart = MakeButton(Strings.Get("BtnStart"), 130, primary: true);
        _btnSettings = MakeButton(Strings.Get("BtnSettings"), 110, primary: false);
        _btnSave = MakeButton(Strings.Get("BtnSave"), 110, primary: false);
        _btnSave.Enabled = false;
        _btnStart.Click += async (_, _) => await RunDiagnosticsAsync();
        _btnSettings.Click += (_, _) => OpenSettings();
        _btnSave.Click += (_, _) => SaveResults();
        buttons.Controls.Add(_btnStart);
        buttons.Controls.Add(_btnSettings);
        buttons.Controls.Add(_btnSave);
        root.Controls.Add(buttons, 0, 0);

        // ---- 総合判定バナー
        _verdictPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorIdleBg,
            Margin = new Padding(0, 4, 0, 4),
            Padding = new Padding(10, 5, 10, 5),
        };
        _verdictLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = Strings.Get("VerdictIdle"),
        };
        _verdictSubLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = ColorNote,
            AutoEllipsis = true,
        };
        _verdictPanel.Controls.Add(_verdictLabel);
        _verdictPanel.Controls.Add(_verdictSubLabel);
        root.Controls.Add(_verdictPanel, 0, 1);

        // ---- チェック一覧
        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
        };
        _list.Columns.Add(Strings.Get("ColStatus"), 56);
        _list.Columns.Add(Strings.Get("ColId"), 72);
        _list.Columns.Add(Strings.Get("ColName"), 190);
        _list.Columns.Add(Strings.Get("ColDetail"), 330);
        _list.Columns.Add(Strings.Get("ColLatency"), 90);
        root.Controls.Add(_list, 0, 2);

        // ---- プログレス
        var progressPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        _progress = new ProgressBar { Dock = DockStyle.Left, Width = 320 };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = ColorNote,
            Padding = new Padding(8, 0, 0, 0),
        };
        progressPanel.Controls.Add(_statusLabel);
        progressPanel.Controls.Add(_progress);
        root.Controls.Add(progressPanel, 0, 3);

        // ---- 原因・ヒント
        _detailLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = ColorNote,
            AutoEllipsis = true,
        };
        root.Controls.Add(_detailLabel, 0, 4);
    }

    private static Button MakeButton(string text, int width, bool primary)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        if (primary)
        {
            b.BackColor = AccentBlue;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = AccentBlue;
        }
        else
        {
            b.BackColor = Color.FromArgb(246, 248, 250);
            b.ForeColor = Color.FromArgb(31, 35, 40);
            b.FlatAppearance.BorderColor = Color.FromArgb(208, 215, 222);
        }
        return b;
    }

    // ---------------- 診断実行 ----------------

    private async Task RunDiagnosticsAsync()
    {
        SetRunning(true);
        _list.Items.Clear();
        _doneChecks = 0;
        _totalChecks = 0;
        _progress.Value = 0;
        _detailLabel.Text = "";
        _verdictPanel.BackColor = ColorIdleBg;
        _verdictLabel.ForeColor = Color.Black;
        _verdictSubLabel.Text = "";

        PrivacyMask.Enabled = _config.PrivacyMode;
        PrivacyMask.Reset();

        var logger = new Logger(_config, enableFile: true);
        var engine = new DiagnosticEngine(_config, _targets, logger);
        engine.CheckStarted += (_, id) => _ = BeginInvoke(new Action(() => OnCheckStarted(id)));
        engine.CheckCompleted += (_, r) => _ = BeginInvoke(new Action(() => OnCheckCompleted(r)));

        try
        {
            var report = await Task.Run(() => engine.RunAsync(null, CancellationToken.None));
            _lastReport = report;
            SaveOutputs(report, logger);
            ShowReport(report);
        }
        catch (Exception ex)
        {
            _verdictPanel.BackColor = ColorFailBg;
            _verdictLabel.ForeColor = ColorFail;
            _verdictLabel.Text = Strings.Get("verdict.UNKNOWN");
            _verdictSubLabel.Text = ex.Message;
        }
        finally
        {
            logger.Dispose();
            SetRunning(false);
        }
    }

    private void OnCheckStarted(string id)
    {
        _totalChecks++;
        var item = new ListViewItem("…");
        item.SubItems.Add(id);
        item.SubItems.Add(Strings.Get($"check.{id}"));
        item.SubItems.Add("");
        item.SubItems.Add("");
        item.ForeColor = ColorUnknown;
        item.Tag = id;
        _list.Items.Add(item);
        item.EnsureVisible();
        UpdateProgressText();
    }

    private void OnCheckCompleted(CheckResult r)
    {
        var item = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Tag as string == r.Id);
        if (item is null) return;

        var (icon, color) = r.Status switch
        {
            CheckStatus.Ok => ("✓", ColorOk),
            CheckStatus.Warning => ("!", ColorWarn),
            CheckStatus.Failed => ("✗", ColorFail),
            CheckStatus.Skipped => ("―", ColorSkip),
            _ => ("?", ColorUnknown),
        };
        item.Text = icon;
        item.ForeColor = color;
        item.SubItems[3].Text = r.Detail + (r.Attempts > 1 ? $" ({Strings.Format("CliAttempts", r.Attempts)})" : "");
        item.SubItems[4].Text = r.LatencyMs is null ? "" : $"{r.LatencyMs} ms";

        _doneChecks++;
        _progress.Value = _totalChecks == 0 ? 0 : Math.Min(100, _doneChecks * 100 / _totalChecks);
        UpdateProgressText();
        item.EnsureVisible();
    }

    private void ShowReport(DiagnosticReport report)
    {
        var (bg, fg) = report.ExitCode switch
        {
            0 => (ColorOkBg, ColorOk),
            1 => (ColorWarnBg, ColorWarn),
            _ => (ColorFailBg, ColorFail),
        };
        _verdictPanel.BackColor = bg;
        _verdictLabel.ForeColor = fg;
        _verdictLabel.Text = $"{Strings.Get("CliOverall")}：{report.VerdictMessage}";
        _verdictSubLabel.Text = report.SuspectedCauses.Count > 0
            ? $"{Strings.Get("CausesTitle")}：{string.Join("／", report.SuspectedCauses)}"
            : "";

        var lines = new List<string>();
        if (report.Hints.Count > 0)
            lines.Add($"{Strings.Get("HintsTitle")}：{string.Join("／", report.Hints)}");
        lines.Add(Strings.Get("Disclaimer"));
        if (_config.PrivacyMode) lines.Add($"※ {Strings.Get("MaskNote")}");
        _detailLabel.Text = string.Join(Environment.NewLine, lines);

        _statusLabel.Text = $"result.json / report.html → {AppPaths.BaseDir}";
        _progress.Value = 100;
        _btnSave.Enabled = true;
    }

    private void SaveOutputs(DiagnosticReport report, Logger logger)
    {
        try
        {
            JsonReporter.Save(report, AppPaths.ResultJsonPath);
            if (_config.GenerateHtmlReport)
                HtmlReporter.Save(report, logger.Entries, AppPaths.ReportHtmlPath, _config.ReportRefreshSeconds);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveResults()
    {
        using var dialog = new SaveFileDialog
        {
            Title = Strings.Get("BtnSave"),
            Filter = "HTML report (*.html)|*.html|JSON (*.json)|*.json",
            FileName = "report.html",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var source = dialog.FilterIndex == 2 ? AppPaths.ResultJsonPath : AppPaths.ReportHtmlPath;
            File.Copy(source, dialog.FileName, overwrite: true);
            MessageBox.Show(this, Strings.Format("SavedTo", dialog.FileName), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config, _targets);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            ConfigLoader.SaveConfig(_config);
            ConfigLoader.SaveTargets(_targets);
            PrivacyMask.Enabled = _config.PrivacyMode;
            Strings.Initialize(_config.Language);
            Relocalize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Relocalize()
    {
        Text = $"{Strings.Get("AppTitle")}  v{VersionInfo.Version}";
        _btnStart.Text = Strings.Get("BtnStart");
        _btnSettings.Text = Strings.Get("BtnSettings");
        _btnSave.Text = Strings.Get("BtnSave");
        _list.Columns[0].Text = Strings.Get("ColStatus");
        _list.Columns[1].Text = Strings.Get("ColId");
        _list.Columns[2].Text = Strings.Get("ColName");
        _list.Columns[3].Text = Strings.Get("ColDetail");
        _list.Columns[4].Text = Strings.Get("ColLatency");
        if (_lastReport is null) _verdictLabel.Text = Strings.Get("VerdictIdle");
    }

    private void SetRunning(bool running)
    {
        _btnStart.Enabled = !running;
        _btnSettings.Enabled = !running;
        _btnStart.Text = running ? Strings.Get("BtnRunning") : Strings.Get("BtnStart");
    }

    private void UpdateProgressText()
    {
        _statusLabel.Text = Strings.Format("ProgressFormat", _doneChecks, _totalChecks);
        _verdictLabel.Text = Strings.Format("VerdictRunning", _doneChecks);
    }
}