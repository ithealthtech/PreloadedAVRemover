using PreloadedAVRemover.Core;
using System.Drawing.Drawing2D;

namespace PreloadedAVRemover;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => StartupCrashReporter.Report(e.Exception, "UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => StartupCrashReporter.Report(e.ExceptionObject as Exception ?? new Exception("Unknown non-UI exception"), "AppDomain");
        try
        {
            if (selfTest) { using var form = new MainForm(); form.CreateControl(); form.RunLayoutSelfTest(); return; }
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            StartupCrashReporter.Report(ex, selfTest ? "Self-test" : "Startup", showDialog: !selfTest);
            if (selfTest) Environment.ExitCode = 1;
        }
    }
}

internal sealed record UiEntry(PlanItem Plan)
{
    public string Name => Plan.Inventory.Name;
    public string Brand => Plan.Catalog.Brand;
    public string Type => Plan.Inventory.PackageType.ToString();
    public string Risk => Plan.Catalog.RiskLevel.ToString();
    public string Confidence => $"{Plan.MatchConfidence}%";
    public string Decision => Plan.Decision.Action.ToString();
    public string Reason => Plan.Decision.Reason;
}

internal sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(15, 23, 42);
    private static readonly Color Slate = Color.FromArgb(71, 85, 105);
    private static readonly Color Canvas = Color.FromArgb(241, 245, 249);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);

    private readonly CheckBox _execute = new() { Text = "Execute removals", AutoSize = true };
    private readonly CheckBox _allowSecurity = new() { Text = "Allow AV removal", AutoSize = true };
    private readonly ComboBox _profile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 118 };
    private readonly Button _scan = new() { Text = "Audit again" };
    private readonly Button _run = new() { Text = "Run dry-run", Enabled = false };
    private readonly Button _export = new() { Text = "Export report", Enabled = false };
    private readonly Label _summary = new() { AutoSize = true, Text = "Ready to audit", Font = new Font("Segoe UI Semibold", 11), ForeColor = Color.FromArgb(30, 41, 59) };
    private readonly Label _resultCount = new() { AutoSize = true, Text = "Dry-run is enabled by default", ForeColor = Slate, Margin = new Padding(0, 4, 0, 0) };
    private readonly DataGridView _grid = new();
    private readonly TextBox _activity = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None, BackColor = Navy, ForeColor = Color.FromArgb(203, 213, 225) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 3, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
    private readonly RowStyle _contentHeaderRow = new(SizeType.Absolute, 100);
    private readonly RowStyle _gridRow = new(SizeType.Percent, 66);
    private readonly RowStyle _logRow = new(SizeType.Percent, 34);
    private readonly CleanupEngine _engine;
    private FlowLayoutPanel _toolbar = null!;
    private TableLayoutPanel _contentLayout = null!;
    private GradientPanel _headerPanel = null!;
    private Label _headerDetail = null!;
    private Label _copyrightFooter = null!;
    private CleanupPolicy _config = new();
    private AuditReport? _report;
    private List<UiEntry> _entries = [];
    private string? _jsonReportPath;
    private string? _htmlReportPath;
    private bool _hasExecuted;

    public MainForm()
    {
        _engine = new CleanupEngine(new WindowsInventoryProvider(), RemovalCatalog.LoadEmbedded(), new ProcessRunner());
        Text = "OEM Endpoint Cleanup 2.2.0";
        MinimumSize = new Size(860, 620);
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Canvas;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        SetStyle(ControlStyles.ResizeRedraw, true);

        _profile.Items.AddRange(Enum.GetNames<PolicyProfile>());
        _config = PolicyConfiguration.Load();
        _profile.SelectedItem = _config.Profile.ToString();
        _execute.Checked = !_config.DryRun;
        _allowSecurity.Checked = _config.AllowSecurityProductRemoval;
        StyleButton(_scan, Color.White, Color.FromArgb(30, 41, 59), Color.FromArgb(203, 213, 225), 108);
        StyleButton(_run, Red, Color.White, Red, 148);
        StyleButton(_export, Color.White, Color.FromArgb(30, 41, 59), Color.FromArgb(203, 213, 225), 118);
        foreach (var check in new[] { _execute, _allowSecurity }) { check.ForeColor = Slate; check.Margin = new Padding(8, 8, 0, 0); }

        var header = BuildHeader();
        var contentHeader = BuildContentHeader();
        ConfigureGrid();
        var gridCard = new BorderedPanel { Dock = DockStyle.Fill, BackColor = Color.White, BorderColor = Color.FromArgb(226, 232, 240), Margin = new Padding(0, 10, 0, 16), Padding = new Padding(1) };
        gridCard.Controls.Add(_grid);
        var activityTitle = new Label { Text = "TAMPER-EVIDENT ACTIVITY LOG", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(148, 163, 184), BackColor = Navy, Padding = new Padding(13, 11, 0, 0) };
        var activityCard = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(14, 0, 14, 14), Margin = new Padding(0) };
        activityCard.Controls.Add(_activity); activityCard.Controls.Add(activityTitle);
        _copyrightFooter = new Label
        {
            Text = "Copyright © 2026 IT Health Tech LLC",
            Dock = DockStyle.Bottom,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Canvas,
            ForeColor = Slate,
            Font = new Font("Segoe UI", 8.5f)
        };
        _contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30, 20, 30, 26), RowCount = 3, ColumnCount = 1, BackColor = Canvas };
        _contentLayout.RowStyles.Add(_contentHeaderRow); _contentLayout.RowStyles.Add(_gridRow); _contentLayout.RowStyles.Add(_logRow);
        _contentLayout.Controls.Add(contentHeader, 0, 0); _contentLayout.Controls.Add(gridCard, 0, 1); _contentLayout.Controls.Add(activityCard, 0, 2);
        Controls.Add(_contentLayout); Controls.Add(_copyrightFooter); Controls.Add(header);

        _scan.Click += async (_, _) => await AuditAsync();
        _run.Click += async (_, _) => await RunSelectedAsync();
        _export.Click += (_, _) => ShowReportLocation();
        _execute.CheckedChanged += (_, _) => { _run.Text = _execute.Checked ? "Execute selected" : "Run dry-run"; UpdateRunButton(); };
        _allowSecurity.CheckedChanged += async (_, _) => await AuditAsync();
        _profile.SelectedIndexChanged += async (_, _) => { if (Visible) await AuditAsync(); };
        _grid.SelectionChanged += (_, _) => UpdateRunButton();
        _grid.CellFormatting += FormatCell;
        Resize += (_, _) => ApplyResponsiveLayout();
        ApplyResponsiveLayout();
        Shown += async (_, _) => await AuditAsync();
    }

    private Control BuildHeader()
    {
        var mark = new ShieldMark { Size = new Size(58, 58), Margin = new Padding(0, 2, 18, 0) };
        var heading = new Label { Text = "OEM Endpoint Cleanup", Font = new Font("Segoe UI Semibold", 22), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0) };
        var version = new Label { Text = "SECURE AUDIT + REMOVAL  /  VERSION 2.2.0", Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(94, 234, 212), AutoSize = true, Margin = new Padding(2, 7, 0, 0) };
        _headerDetail = new Label { Text = "Inventory OEM software, apply policy, validate every command, and produce MSP-ready evidence.", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(203, 213, 225), AutoSize = true, Margin = new Padding(2, 7, 0, 0) };
        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
        text.Controls.AddRange([heading, version, _headerDetail]);
        var inner = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(30, 23, 30, 18) };
        inner.Controls.AddRange([mark, text]);
        _headerPanel = new GradientPanel { Dock = DockStyle.Top, Height = 132 }; _headerPanel.Controls.Add(inner); _headerPanel.Controls.Add(_progress); return _headerPanel;
    }

    private Control BuildContentHeader()
    {
        var status = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        status.Controls.AddRange([_summary, _resultCount]);
        var profileLabel = new Label { Text = "Policy", AutoSize = true, ForeColor = Slate, Margin = new Padding(0, 8, 6, 0) };
        var protectedBadge = new Label { Text = "PROTECTED SOFTWARE GUARDED", AutoSize = true, BackColor = Color.FromArgb(220, 252, 231), ForeColor = Color.FromArgb(22, 101, 52), Font = new Font("Segoe UI Semibold", 8), Padding = new Padding(9, 7, 9, 7), Margin = new Padding(12, 3, 0, 0) };
        _toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0), Padding = new Padding(0, 6, 0, 0) };
        _toolbar.Controls.AddRange([profileLabel, _profile, _scan, _run, _export, _execute, _allowSecurity]);
        var summaryRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); summaryRow.Controls.Add(status, 0, 0); summaryRow.Controls.Add(protectedBadge, 1, 0);
        var result = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), AutoSize = false };
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 43)); result.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); result.Controls.Add(summaryRow, 0, 0); result.Controls.Add(_toolbar, 0, 1); return result;
    }

    private void ApplyResponsiveLayout()
    {
        if (_contentLayout is null || _toolbar is null || _headerPanel is null) return;
        var narrow = ClientSize.Width < 1080;
        var shortWindow = ClientSize.Height < 720;
        _contentLayout.Padding = narrow ? new Padding(18, 14, 18, 18) : new Padding(30, 20, 30, 26);
        _contentHeaderRow.Height = narrow ? 132 : 96;
        _toolbar.WrapContents = narrow;
        _headerPanel.Height = narrow ? 116 : 132;
        _headerDetail.Visible = !narrow;
        _gridRow.Height = shortWindow ? 74 : 66;
        _logRow.Height = shortWindow ? 26 : 34;
        _grid.Columns[nameof(UiEntry.Brand)].Visible = ClientSize.Width >= 940;
        _grid.Columns[nameof(UiEntry.Type)].Visible = ClientSize.Width >= 900;
        _grid.Columns[nameof(UiEntry.Confidence)].Visible = ClientSize.Width >= 1120;
        _contentLayout.PerformLayout();
    }

    internal void RunLayoutSelfTest()
    {
        foreach (var size in new[] { new Size(860, 620), new Size(1024, 700), new Size(1280, 820), new Size(1600, 1000) })
        {
            ClientSize = size;
            ApplyResponsiveLayout();
            PerformLayout();
            if (_toolbar.Width <= 0 || _toolbar.Height <= 0 || _grid.Width <= 0 || _activity.Width <= 0 ||
                _copyrightFooter.Width <= 0 || _copyrightFooter.Height <= 0 || string.IsNullOrWhiteSpace(_copyrightFooter.Text))
                throw new InvalidOperationException($"Responsive layout failed at {size.Width}x{size.Height}.");
        }
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill; _grid.AutoGenerateColumns = false; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect = true; _grid.RowHeadersVisible = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BorderStyle = BorderStyle.None; _grid.BackgroundColor = Color.White; _grid.GridColor = Color.FromArgb(226, 232, 240); _grid.EnableHeadersVisualStyles = false; _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; _grid.ColumnHeadersHeight = 42;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), ForeColor = Slate, Font = new Font("Segoe UI Semibold", 9), Padding = new Padding(8, 0, 8, 0), SelectionBackColor = Color.FromArgb(248, 250, 252), SelectionForeColor = Slate };
        _grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Color.FromArgb(30, 41, 59), SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(12, 74, 110), Padding = new Padding(8, 5, 8, 5) };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(12, 74, 110), Padding = new Padding(8, 5, 8, 5) }; _grid.RowTemplate.Height = 42;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Name), DataPropertyName = nameof(UiEntry.Name), HeaderText = "PRODUCT", FillWeight = 29 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Brand), DataPropertyName = nameof(UiEntry.Brand), HeaderText = "BRAND", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Type), DataPropertyName = nameof(UiEntry.Type), HeaderText = "TYPE", FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Risk), DataPropertyName = nameof(UiEntry.Risk), HeaderText = "RISK", FillWeight = 11 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Confidence), DataPropertyName = nameof(UiEntry.Confidence), HeaderText = "MATCH", FillWeight = 9 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Decision), DataPropertyName = nameof(UiEntry.Decision), HeaderText = "DECISION", FillWeight = 13 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Reason), DataPropertyName = nameof(UiEntry.Reason), HeaderText = "POLICY REASON", FillWeight = 25 });
    }

    private async Task AuditAsync()
    {
        SetBusy(true); _hasExecuted = false;
        try
        {
            _config = PolicyConfiguration.Load();
            var policy = CurrentPolicy(); var id = Guid.NewGuid().ToString("N"); var directory = ReportDirectory(policy);
            var logPath = Path.Combine(directory, $"audit-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{id[..8]}.jsonl");
            Log($"Audit started. Profile={policy.Profile}; Mode={(policy.DryRun ? "Dry-run" : "Removal")}; SecurityRemoval={policy.AllowSecurityProductRemoval}");
            _report = await Task.Run(() => _engine.Audit(policy, id, logPath));
            _report.Results = _report.Before.Select(x => new ExecutionResult(x, ExecutionOutcome.Detected, null, x.Decision.Reason, false, DateTimeOffset.UtcNow)).ToList();
            _report.After = _report.Before; _report.AfterInventory = _report.FullInventory; _report.CompletedAt = DateTimeOffset.UtcNow;
            _report.Summary = BuildSummary(_report); _report.AuditLogSha256 = HashChainAuditLogger.Sha256File(logPath);
            (_jsonReportPath, _htmlReportPath) = ReportWriter.Write(_report, directory);
            _entries = _report.Before.Select(x => new UiEntry(x)).ToList(); _grid.DataSource = _entries;
            _summary.Text = _entries.Count == 0 ? "All clear" : "Review policy decisions";
            _resultCount.Text = $"{_entries.Count} catalog match(es) across {_report.FullInventory.Count} inventoried item(s) - {_report.Device.Manufacturer} {_report.Device.Model}";
            Log($"Audit complete. Matches={_entries.Count}; Inventory={_report.FullInventory.Count}; Admin={_report.Device.IsAdministrator}; RebootPending={_report.Device.RebootPending}");
            Log($"Reports: {_jsonReportPath} | {_htmlReportPath}"); _export.Enabled = true;
        }
        catch (Exception ex) { Log("AUDIT FAILED: " + ex.Message); MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetBusy(false); }
    }

    private async Task RunSelectedAsync()
    {
        if (_report is null) return;
        var selected = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as UiEntry).Where(x => x?.Plan.Decision.Action == DecisionAction.Remove).Cast<UiEntry>().Select(x => x.Plan).ToList();
        if (selected.Count == 0) { MessageBox.Show("Select one or more entries whose policy decision is Remove.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var policy = CurrentPolicy();
        if (!policy.DryRun && !policy.Force)
        {
            var securityCount = selected.Count(x => x.Catalog.IsSecurityProduct);
            var warning = $"This will execute validated uninstallers for {selected.Count} selected product(s)." + (securityCount > 0 ? $"{Environment.NewLine}{securityCount} endpoint-protection product(s) are included." : "") + $"{Environment.NewLine}{Environment.NewLine}Continue?";
            if (MessageBox.Show(warning, "Confirm removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        }
        SetBusy(true);
        try
        {
            var selectedResults = await _engine.ExecuteAsync(selected, policy, _report.ExecutionId, _report.AuditLogPath!);
            var selectedKeys = selected.Select(x => x.Catalog.Id + "|" + x.Inventory.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unselected = _report.Before.Where(x => !selectedKeys.Contains(x.Catalog.Id + "|" + x.Inventory.Id)).Select(x => new ExecutionResult(x, ExecutionOutcome.Skipped, null, x.Decision.Action == DecisionAction.Remove ? "Not selected" : x.Decision.Reason, false, DateTimeOffset.UtcNow));
            _report.Results = selectedResults.Concat(unselected).ToList();
            var after = await Task.Run(() => _engine.RescanFull(policy)); _report.AfterInventory = after.Inventory; _report.After = after.Plan; _report.CompletedAt = DateTimeOffset.UtcNow;
            _report.Summary = BuildSummary(_report);
            _report.RollbackGuidance = selectedResults.Where(x => x.Outcome is ExecutionOutcome.Removed or ExecutionOutcome.RebootRequired).Select(x => $"{x.Plan.Inventory.Name}: reinstall from the OEM support site, Microsoft Store, or the organization's approved software source. Automatic rollback is not available for vendor uninstallers.").ToArray();
            _report.ExecutionLogPath = CleanupEngine.ExecutionLogPath(_report.AuditLogPath!);
            if (File.Exists(_report.ExecutionLogPath)) _report.ExecutionLogSha256 = HashChainAuditLogger.Sha256File(_report.ExecutionLogPath);
            (_jsonReportPath, _htmlReportPath) = ReportWriter.Write(_report, ReportDirectory(policy));
            foreach (var result in selectedResults) Log($"{result.Outcome}: {result.Plan.Inventory.Name}; Exit={result.ExitCode?.ToString() ?? "n/a"}; {result.Message}; Reboot={result.RebootRequired}");
            _summary.Text = policy.DryRun ? "Dry-run complete - no changes made" : "Removal pass complete";
            _resultCount.Text = $"Before matches: {_report.Before.Count}; after matches: {_report.After.Count}; failures/timeouts: {_report.Results.Count(x => x.Outcome is ExecutionOutcome.Failed or ExecutionOutcome.TimedOut)}";
            _hasExecuted = true; _export.Enabled = true;
        }
        catch (Exception ex) { Log("EXECUTION FAILED: " + ex.Message); MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetBusy(false); }
    }

    private CleanupPolicy CurrentPolicy() => new()
    {
        Profile = Enum.TryParse<PolicyProfile>(_profile.SelectedItem?.ToString(), out var p) ? p : PolicyProfile.Conservative,
        DryRun = !_execute.Checked,
        Force = _config.Force,
        AllowSecurityProductRemoval = _allowSecurity.Checked,
        ProcessTimeoutSeconds = _config.ProcessTimeoutSeconds,
        AllowList = _config.AllowList,
        BlockList = _config.BlockList,
        ReportDirectory = string.IsNullOrWhiteSpace(_config.ReportDirectory) ? PolicyConfiguration.DefaultReportDirectory() : _config.ReportDirectory
    };

    private static Dictionary<string, int> BuildSummary(AuditReport report)
    {
        var summary = new Dictionary<string, int>
        {
            ["Inventory.Before"] = report.FullInventory.Count,
            ["Inventory.After"] = report.AfterInventory.Count,
            ["Matches.Before"] = report.Before.Count,
            ["Matches.After"] = report.After.Count
        };
        foreach (var group in report.Before.GroupBy(x => x.Catalog.RiskLevel)) summary[$"Risk.{group.Key}"] = group.Count();
        foreach (var group in report.Before.GroupBy(x => x.Decision.Action)) summary[$"Decision.{group.Key}"] = group.Count();
        foreach (var group in report.Results.GroupBy(x => x.Outcome)) summary[$"Outcome.{group.Key}"] = group.Count();
        return summary;
    }
    private static string ReportDirectory(CleanupPolicy p) => string.IsNullOrWhiteSpace(p.ReportDirectory) ? PolicyConfiguration.DefaultReportDirectory() : p.ReportDirectory;
    private void ShowReportLocation() => MessageBox.Show($"JSON:{Environment.NewLine}{_jsonReportPath}{Environment.NewLine}{Environment.NewLine}HTML:{Environment.NewLine}{_htmlReportPath}", "Audit reports", MessageBoxButtons.OK, MessageBoxIcon.Information);
    private void Log(string message) => _activity.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");

    private void SetBusy(bool busy) { UseWaitCursor = busy; _progress.Visible = busy; _scan.Enabled = !busy; _profile.Enabled = !busy; _execute.Enabled = !busy; _allowSecurity.Enabled = !busy; if (busy) _run.Enabled = false; else UpdateRunButton(); }
    private void UpdateRunButton() { var has = _grid.SelectedRows.Cast<DataGridViewRow>().Any(r => r.DataBoundItem is UiEntry x && x.Plan.Decision.Action == DecisionAction.Remove); _run.Enabled = !UseWaitCursor && !_hasExecuted && has; }
    private void FormatCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        var property = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (property == nameof(UiEntry.Risk) && e.Value is string risk) e.CellStyle!.ForeColor = risk == nameof(RiskLevel.Safe) ? Color.FromArgb(5, 150, 105) : risk == nameof(RiskLevel.Caution) ? Color.FromArgb(217, 119, 6) : Red;
        if (property == nameof(UiEntry.Decision) && e.Value is string decision) e.CellStyle!.ForeColor = decision == nameof(DecisionAction.Remove) ? Red : Slate;
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor, Color borderColor, int width) { button.AutoSize = false; button.Size = new Size(width, 36); button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 1; button.FlatAppearance.BorderColor = borderColor; button.BackColor = backColor; button.ForeColor = foreColor; button.Font = new Font("Segoe UI Semibold", 9); button.Cursor = Cursors.Hand; button.Margin = new Padding(0, 0, 10, 0); }
}

internal static class StartupCrashReporter
{
    private static int _reporting;
    public static void Report(Exception exception, string stage, bool showDialog = true)
    {
        if (Interlocked.Exchange(ref _reporting, 1) != 0) return;
        string path;
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OemCleanup", "CrashLogs");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, $"crash-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, $"TimestampUtc: {DateTimeOffset.UtcNow:O}{Environment.NewLine}Stage: {stage}{Environment.NewLine}Hostname: {Environment.MachineName}{Environment.NewLine}User: {Environment.UserName}{Environment.NewLine}OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}{Environment.NewLine}Process: {Environment.ProcessPath}{Environment.NewLine}Version: 2.2.0{Environment.NewLine}{Environment.NewLine}{exception}");
        }
        catch
        {
            path = Path.Combine(Path.GetTempPath(), "OEMEndpointCleanup-crash.log");
            try { File.WriteAllText(path, exception.ToString()); } catch { path = "Unable to write crash log"; }
        }
        if (showDialog)
        {
            try { MessageBox.Show($"OEM Endpoint Cleanup encountered an unexpected error.{Environment.NewLine}{Environment.NewLine}Diagnostic log:{Environment.NewLine}{path}", "OEM Endpoint Cleanup", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
        Interlocked.Exchange(ref _reporting, 0);
    }
}

internal sealed class GradientPanel : Panel
{
    public GradientPanel() => DoubleBuffered = true;
    protected override void OnPaintBackground(PaintEventArgs e) { using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(15, 23, 42), Color.FromArgb(17, 94, 89), 0f); e.Graphics.FillRectangle(brush, ClientRectangle); }
}

internal sealed class ShieldMark : Control
{
    public ShieldMark() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); DoubleBuffered = true; BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var glow = new SolidBrush(Color.FromArgb(42, 255, 255, 255)); e.Graphics.FillEllipse(glow, 0, 0, Width - 1, Height - 1); using var pen = new Pen(Color.FromArgb(94, 234, 212), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round }; var shield = new[] { new Point(Width / 2, 11), new Point(43, 17), new Point(40, 37), new Point(Width / 2, 47), new Point(16, 37), new Point(13, 17) }; e.Graphics.DrawPolygon(pen, shield); e.Graphics.DrawLines(pen, new Point[] { new(20, 28), new(26, 34), new(37, 22) }); }
}

internal sealed class BorderedPanel : Panel
{
    public Color BorderColor { get; set; } = Color.LightGray;
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(BorderColor); e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
}
