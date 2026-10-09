// File: src/NetworkChecker/Gui/SettingsForm.cs
using NetworkChecker.Core;

namespace NetworkChecker.Gui;

/// <summary>config.json / targets.json の編集画面。</summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly List<TargetDefinition> _targets;

    private readonly NumericUpDown _numTimeout;
    private readonly NumericUpDown _numRetry;
    private readonly NumericUpDown _numRetryInterval;
    private readonly NumericUpDown _numRefresh;
    private readonly NumericUpDown _numRetention;
    private readonly CheckBox _chkIpv6;
    private readonly CheckBox _chkHttp;
    private readonly CheckBox _chkCert;
    private readonly CheckBox _chkLogs;
    private readonly CheckBox _chkReport;
    private readonly CheckBox _chkPrivacy;
    private readonly ComboBox _cmbLang;
    private readonly TextBox _txtTestDomain;
    private readonly TextBox _txtBaseline;
    private readonly TextBox _txtRefDns;
    private readonly TextBox _txtPingTargets;
    private readonly TextBox _txtNtp;
    private readonly DataGridView _grid;

    public SettingsForm(AppConfig config, List<TargetDefinition> targets)
    {
        _config = config;
        _targets = targets;

        Text = Strings.Get("BtnSettings");
        ClientSize = new Size(620, 720);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // ---- 下部ボタン
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(0, 6, 10, 0),
        };
        var btnSave = new Button { Text = Strings.Get("BtnOk"), Width = 100, Height = 30 };
        var btnCancel = new Button { Text = Strings.Get("BtnCancel"), Width = 100, Height = 30, DialogResult = DialogResult.Cancel };
        btnSave.Click += (_, _) =>
        {
            Save();
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(btnSave);
        buttons.Controls.Add(btnCancel);

        // ---- 設定項目
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(12, 12, 12, 0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(string labelKey, Control control, int height = 28)
        {
            var rowIndex = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height + 4));
            panel.Controls.Add(new Label
            {
                Text = Strings.Get(labelKey),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 8, 0),
            }, 0, rowIndex);
            control.Dock = DockStyle.Top;
            panel.Controls.Add(control, 1, rowIndex);
        }

        static NumericUpDown Num(int min, int max, int value, int increment = 1) => new()
        {
            Minimum = min,
            Maximum = max,
            Increment = increment,
            Value = Math.Clamp(value, min, max),
            Width = 120,
        };
        static TextBox Txt(string text) => new() { Text = text };
        static CheckBox Chk(bool value) => new() { Checked = value, Text = "" };

        _numTimeout = Num(500, 60000, config.TimeoutMs, 500);
        Row("set.timeout", _numTimeout);
        _numRetry = Num(1, 5, config.RetryCount);
        Row("set.retry", _numRetry);
        _numRetryInterval = Num(0, 10000, config.RetryIntervalMs, 100);
        Row("set.retryInterval", _numRetryInterval);
        _numRefresh = Num(0, 3600, config.ReportRefreshSeconds, 30);
        Row("set.refresh", _numRefresh);
        _numRetention = Num(1, 365, config.LogRetentionDays);
        Row("set.retention", _numRetention);

        _cmbLang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _cmbLang.Items.AddRange(new object[] { Strings.Get("set.langAuto"), "日本語", "English" });
        _cmbLang.SelectedIndex = config.Language switch { "ja" => 1, "en" => 2, _ => 0 };
        Row("set.lang", _cmbLang);

        _chkIpv6 = Chk(config.EnableIpv6);
        Row("set.ipv6", _chkIpv6);
        _chkHttp = Chk(config.EnableHttpTest);
        Row("set.http", _chkHttp);
        _chkCert = Chk(config.EnableCertificateTest);
        Row("set.cert", _chkCert);
        _chkLogs = Chk(config.SaveLogs);
        Row("set.logs", _chkLogs);
        _chkReport = Chk(config.GenerateHtmlReport);
        Row("set.report", _chkReport);
        _chkPrivacy = Chk(config.PrivacyMode);
        Row("set.privacy", _chkPrivacy);

        _txtTestDomain = Txt(config.TestDomain);
        Row("set.testDomain", _txtTestDomain);
        _txtBaseline = Txt(config.HttpsBaselineUrl);
        Row("set.baseline", _txtBaseline);
        _txtRefDns = Txt(string.Join(", ", config.ReferenceDnsServers));
        Row("set.refDns", _txtRefDns);
        _txtPingTargets = Txt(string.Join(", ", config.ExternalPingTargets));
        Row("set.pingTargets", _txtPingTargets);
        _txtNtp = Txt(string.Join(", ", config.NtpServers));
        Row("set.ntp", _txtNtp);

        // ---- targets 編集グリッド
        var targetsLabel = new Label
        {
            Text = Strings.Get("set.targets"),
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(12, 6, 0, 0),
            Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
        };

        var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 12) };
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            RowHeadersVisible = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = Strings.Get("set.colEnabled"), FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.Get("set.colName"), FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "URL", FillWeight = 38 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.Get("set.colExpected"), FillWeight = 17 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.Get("set.colTimeout"), FillWeight = 11 });
        foreach (var t in _targets)
        {
            _grid.Rows.Add(t.Enabled, t.Name, t.Url, string.Join(",", t.ExpectedStatus), t.TimeoutMs);
        }
        gridPanel.Controls.Add(_grid);

        Controls.Add(gridPanel);
        Controls.Add(targetsLabel);
        Controls.Add(panel);
        Controls.Add(buttons);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void Save()
    {
        _config.TimeoutMs = (int)_numTimeout.Value;
        _config.RetryCount = (int)_numRetry.Value;
        _config.RetryIntervalMs = (int)_numRetryInterval.Value;
        _config.ReportRefreshSeconds = (int)_numRefresh.Value;
        _config.LogRetentionDays = (int)_numRetention.Value;
        _config.EnableIpv6 = _chkIpv6.Checked;
        _config.EnableHttpTest = _chkHttp.Checked;
        _config.EnableCertificateTest = _chkCert.Checked;
        _config.SaveLogs = _chkLogs.Checked;
        _config.GenerateHtmlReport = _chkReport.Checked;
        _config.PrivacyMode = _chkPrivacy.Checked;
        _config.Language = _cmbLang.SelectedIndex switch { 1 => "ja", 2 => "en", _ => null };
        _config.TestDomain = _txtTestDomain.Text.Trim();
        _config.HttpsBaselineUrl = _txtBaseline.Text.Trim();
        _config.ReferenceDnsServers = SplitCsv(_txtRefDns.Text);
        _config.ExternalPingTargets = SplitCsv(_txtPingTargets.Text);
        _config.NtpServers = SplitCsv(_txtNtp.Text);

        _grid.EndEdit();
        _targets.Clear();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            var name = row.Cells[1].Value?.ToString()?.Trim() ?? "";
            var url = row.Cells[2].Value?.ToString()?.Trim() ?? "";
            if (name.Length == 0 || url.Length == 0) continue;

            _targets.Add(new TargetDefinition
            {
                Enabled = row.Cells[0].Value is bool b && b,
                Name = name,
                Url = url,
                ExpectedStatus = ParseStatusList(row.Cells[3].Value?.ToString()),
                TimeoutMs = int.TryParse(row.Cells[4].Value?.ToString(), out var t) && t >= 500 ? t : 5000,
            });
        }
    }

    private static string[] SplitCsv(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int[] ParseStatusList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new[] { 200, 301, 302 };
        var list = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .Where(n => n is >= 100 and <= 599)
            .Select(n => n!.Value)
            .ToArray();
        return list.Length > 0 ? list : new[] { 200, 301, 302 };
    }
}