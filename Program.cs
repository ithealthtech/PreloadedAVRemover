using PreloadedAVRemover.Core;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PreloadedAVRemover;

public static class ProductInfo
{
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "Unknown";
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
        var uiPreview = args.Contains("--ui-preview", StringComparer.OrdinalIgnoreCase);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => StartupCrashReporter.Report(e.Exception, "UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => StartupCrashReporter.Report(e.ExceptionObject as Exception ?? new Exception("Unknown non-UI exception"), "AppDomain");
        try
        {
            if (selfTest) { using var form = new MainForm(); form.CreateControl(); form.RunLayoutSelfTest(); return; }
            Application.Run(new MainForm(uiPreview));
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
    public string Name => FriendlyDisplay.ProductName(Plan.Inventory.Name);
    public string Category => FriendlyDisplay.CategoryLabel(Plan);
    public string Brand => Plan.Catalog.Brand == "Any" ? "Various" : Plan.Catalog.Brand;
    public string Type => FriendlyDisplay.PackageTypeLabel(Plan.Inventory.PackageType);
    public string Risk => FriendlyDisplay.RiskLabel(Plan.Catalog.RiskLevel);
    public string Confidence => $"{Plan.MatchConfidence}%";
    public string Decision => FriendlyDisplay.DecisionLabel(Plan.Decision.Action);
    public string Reason => Plan.Decision.Reason;
    public string RemoteDisposition => SoftwareClassification.RemoteDisposition(Plan);
}

internal sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(15, 23, 42);
    private static readonly Color Slate = Color.FromArgb(71, 85, 105);
    private static readonly Color Canvas = Color.FromArgb(241, 245, 249);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);

    private readonly CheckBox _execute = new() { Text = "Uninstall mode", AutoSize = true };
    private readonly CheckBox _allowSecurity = new() { Text = "Include security apps", AutoSize = true };
    private readonly CheckBox _allowRemote = new() { Text = "Include remote tools", AutoSize = true };
    private readonly ComboBox _profile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 118 };
    private readonly Button _scan = new() { Text = "Audit again" };
    private readonly Button _run = new() { Text = "Preview selected", Enabled = false };
    private readonly Button _export = new() { Text = "Export report", Enabled = false };
    private readonly Button _toggleLog = new() { Text = "Show activity" };
    private readonly Label _summary = new() { AutoSize = true, Text = "Ready to audit", Font = new Font("Segoe UI Semibold", 11), ForeColor = Color.FromArgb(30, 41, 59) };
    private readonly Label _resultCount = new() { AutoSize = true, Text = "Dry-run is enabled by default", ForeColor = Slate, Margin = new Padding(0, 4, 0, 0) };
    private readonly DataGridView _grid = new();
    private readonly DataGridView _remoteGrid = new();
    private readonly Label _oemCount = new() { AutoSize = true, Text = "Waiting for audit", ForeColor = Slate, Font = new Font("Segoe UI", 8.5f) };
    private readonly Label _remoteCount = new() { AutoSize = true, Text = "Waiting for audit", ForeColor = Slate, Font = new Font("Segoe UI", 8.5f) };
    private readonly TextBox _activity = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None, BackColor = Navy, ForeColor = Color.FromArgb(203, 213, 225) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 3, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
    private readonly RowStyle _contentHeaderRow = new(SizeType.Absolute, 100);
    private readonly RowStyle _gridRow = new(SizeType.Percent, 66);
    private readonly RowStyle _logRow = new(SizeType.Percent, 34);
    private readonly CleanupEngine _engine;
    private readonly bool _uiPreview;
    private FlowLayoutPanel _toolbar = null!;
    private TableLayoutPanel _contentLayout = null!;
    private SplitContainer _inventorySplit = null!;
    private GradientPanel _headerPanel = null!;
    private Label _headerDetail = null!;
    private Control _copyrightFooter = null!;
    private CleanupPolicy _config = new();
    private AuditReport? _report;
    private List<UiEntry> _entries = [];
    private List<UiEntry> _remoteEntries = [];
    private string? _jsonReportPath;
    private string? _htmlReportPath;
    private bool _hasExecuted;
    private bool _activityVisible;

    public MainForm(bool uiPreview = false)
    {
        _uiPreview = uiPreview;
        _engine = new CleanupEngine(new WindowsInventoryProvider(), RemovalCatalog.LoadEmbedded(), new ProcessRunner());
        Text = $"OEM Endpoint Cleanup {ProductInfo.Version}";
        Icon = LoadAppIcon();
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
        _allowRemote.Checked = _config.AllowRemoteManagementRemoval;
        StyleButton(_scan, Color.White, Color.FromArgb(30, 41, 59), Color.FromArgb(203, 213, 225), 108);
        StyleButton(_run, Color.FromArgb(2, 132, 199), Color.White, Color.FromArgb(2, 132, 199), 148);
        StyleButton(_export, Color.White, Color.FromArgb(30, 41, 59), Color.FromArgb(203, 213, 225), 118);
        StyleButton(_toggleLog, Color.White, Color.FromArgb(30, 41, 59), Color.FromArgb(203, 213, 225), 116);
        foreach (var check in new[] { _execute, _allowSecurity, _allowRemote }) { check.ForeColor = Slate; check.Margin = new Padding(8, 8, 0, 0); }

        var header = BuildHeader();
        var contentHeader = BuildContentHeader();
        ConfigureGrid(_grid, remoteTools: false);
        ConfigureGrid(_remoteGrid, remoteTools: true);
        _inventorySplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            BackColor = Canvas,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 10, 0, 16)
        };
        _inventorySplit.Panel1.Padding = new Padding(0, 0, 4, 0);
        _inventorySplit.Panel2.Padding = new Padding(4, 0, 0, 0);
        _inventorySplit.Panel1.Controls.Add(BuildInventorySection("OEM & optional software", "Bloatware, trials, security, and manufacturer utilities", _oemCount, _grid));
        _inventorySplit.Panel2.Controls.Add(BuildInventorySection("Remote & potentially unwanted tools", "ConnectWise is approved; other tools require investigation", _remoteCount, _remoteGrid));
        var activityTitle = new Label { Text = "TAMPER-EVIDENT ACTIVITY LOG", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(148, 163, 184), BackColor = Navy, Padding = new Padding(13, 11, 0, 0) };
        var activityCard = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(14, 0, 14, 14), Margin = new Padding(0) };
        activityCard.Controls.Add(_activity); activityCard.Controls.Add(activityTitle);
        _copyrightFooter = BuildCopyrightFooter();
        _logRow.SizeType = SizeType.Absolute; _logRow.Height = 0;
        _contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30, 18, 30, 20), RowCount = 3, ColumnCount = 1, BackColor = Canvas };
        _contentLayout.RowStyles.Add(_contentHeaderRow); _contentLayout.RowStyles.Add(_gridRow); _contentLayout.RowStyles.Add(_logRow);
        _contentLayout.Controls.Add(contentHeader, 0, 0); _contentLayout.Controls.Add(_inventorySplit, 0, 1); _contentLayout.Controls.Add(activityCard, 0, 2);
        Controls.Add(_contentLayout); Controls.Add(_copyrightFooter); Controls.Add(header);

        _scan.Click += async (_, _) => await AuditAsync();
        _run.Click += async (_, _) => await RunSelectedAsync();
        _export.Click += (_, _) => ShowReportLocation();
        _execute.CheckedChanged += (_, _) =>
        {
            _run.Text = _execute.Checked ? "Uninstall selected" : "Preview selected";
            _run.BackColor = _execute.Checked ? Red : Color.FromArgb(2, 132, 199);
            _run.FlatAppearance.BorderColor = _run.BackColor;
            _allowSecurity.Visible = _execute.Checked;
            _allowRemote.Visible = _execute.Checked;
            UpdateRunButton();
        };
        _toggleLog.Click += (_, _) => ToggleActivity();
        _allowSecurity.CheckedChanged += async (_, _) => await AuditAsync();
        _allowRemote.CheckedChanged += async (_, _) => await AuditAsync();
        _profile.SelectedIndexChanged += async (_, _) => { if (Visible) await AuditAsync(); };
        _grid.SelectionChanged += (_, _) => UpdateRunButton();
        _remoteGrid.SelectionChanged += (_, _) => UpdateRunButton();
        _grid.CellFormatting += FormatCell;
        _grid.CellToolTipTextNeeded += ShowTechnicalName;
        _remoteGrid.CellFormatting += FormatCell;
        _remoteGrid.CellToolTipTextNeeded += ShowTechnicalName;
        Resize += (_, _) => ApplyResponsiveLayout();
        ApplyResponsiveLayout();
        Shown += async (_, _) =>
        {
            if (_uiPreview) LoadUiPreview();
            else await AuditAsync();
        };
    }

    private Control BuildHeader()
    {
        var mark = new PictureBox { Image = LoadBrandImage("ItHealthTechMark"), Size = new Size(44, 44), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 1, 14, 0) };
        var heading = new Label { Text = "OEM Endpoint Cleanup", Font = new Font("Segoe UI Semibold", 19), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0) };
        var version = new Label { Text = $"SECURE AUDIT + REMOVAL  /  VERSION {ProductInfo.Version}", Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(94, 234, 212), AutoSize = true, Margin = new Padding(2, 7, 0, 0) };
        _headerDetail = new Label { Text = "Audit first. Uninstall only what policy approves.", Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(203, 213, 225), AutoSize = true, Margin = new Padding(2, 5, 0, 0) };
        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
        text.Controls.AddRange([heading, version, _headerDetail]);
        var inner = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(30, 17, 30, 12) };
        inner.Controls.AddRange([mark, text]);
        _headerPanel = new GradientPanel { Dock = DockStyle.Top, Height = 104 }; _headerPanel.Controls.Add(inner); _headerPanel.Controls.Add(_progress); return _headerPanel;
    }

    private Control BuildContentHeader()
    {
        var status = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        status.Controls.AddRange([_summary, _resultCount]);
        var profileLabel = new Label { Text = "Policy", AutoSize = true, ForeColor = Slate, Margin = new Padding(0, 8, 6, 0) };
        _toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0), Padding = new Padding(0, 6, 0, 0) };
        _allowSecurity.Visible = _execute.Checked;
        _allowRemote.Visible = _execute.Checked;
        _toolbar.Controls.AddRange([profileLabel, _profile, _scan, _run, _export, _toggleLog, _execute, _allowSecurity, _allowRemote]);
        var summaryRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); summaryRow.Controls.Add(status, 0, 0);
        var result = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), AutoSize = false };
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 43)); result.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); result.Controls.Add(summaryRow, 0, 0); result.Controls.Add(_toolbar, 0, 1); return result;
    }

    private static Control BuildInventorySection(string title, string subtitle, Label count, DataGridView grid)
    {
        var heading = new Label { Text = title, UseMnemonic = false, AutoSize = true, Font = new Font("Segoe UI Semibold", 10.5f), ForeColor = Color.FromArgb(30, 41, 59), Margin = new Padding(0) };
        var detail = new Label { Text = subtitle, AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = Slate, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 3, 0, 0) };
        count.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        count.TextAlign = ContentAlignment.TopRight;
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, ColumnCount = 2, RowCount = 2, BackColor = Color.White, Padding = new Padding(12, 9, 12, 5) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(heading, 0, 0); header.Controls.Add(count, 1, 0); header.Controls.Add(detail, 0, 1); header.SetColumnSpan(detail, 2);
        var card = new BorderedPanel { Dock = DockStyle.Fill, BackColor = Color.White, BorderColor = Color.FromArgb(203, 213, 225), Margin = new Padding(0), Padding = new Padding(1) };
        card.Controls.Add(grid); card.Controls.Add(header);
        return card;
    }

    private static Control BuildCopyrightFooter()
    {
        var logo = new PictureBox { Image = LoadBrandImage("ItHealthTechLogo"), Size = new Size(132, 27), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 3, 10, 0) };
        var notice = new Label { Text = "© 2026 IT Health Tech LLC  •  GPL-3.0-only", AutoSize = true, ForeColor = Slate, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 8, 0, 0) };
        var content = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        content.Controls.AddRange([logo, notice]);
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 38, ColumnCount = 3, RowCount = 1, BackColor = Canvas };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.Controls.Add(content, 1, 0);
        return footer;
    }

    private static Image LoadBrandImage(string resourceName)
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Brand resource '{resourceName}' is missing.");
        return new Bitmap(stream);
    }

    private static Icon LoadAppIcon()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("ItHealthTechIcon")
            ?? throw new InvalidOperationException("Application icon resource is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private void ApplyResponsiveLayout()
    {
        if (_contentLayout is null || _toolbar is null || _headerPanel is null || _inventorySplit is null) return;
        var narrow = ClientSize.Width < 1080;
        var shortWindow = ClientSize.Height < 720;
        _contentLayout.Padding = narrow ? new Padding(18, 12, 18, 16) : new Padding(30, 18, 30, 20);
        _contentHeaderRow.Height = narrow ? 126 : 90;
        _toolbar.WrapContents = narrow;
        _headerPanel.Height = narrow ? 96 : 104;
        _headerDetail.Visible = !narrow;
        _gridRow.SizeType = SizeType.Percent; _gridRow.Height = 100;
        _logRow.SizeType = SizeType.Absolute; _logRow.Height = _activityVisible ? (shortWindow ? 120 : 190) : 0;
        _grid.Columns[nameof(UiEntry.Brand)].Visible = ClientSize.Width >= 1440;
        _grid.Columns[nameof(UiEntry.Type)].Visible = ClientSize.Width >= 1060;
        _grid.Columns[nameof(UiEntry.Confidence)].Visible = ClientSize.Width >= 1600;
        _grid.Columns[nameof(UiEntry.Reason)].Visible = ClientSize.Width >= 1600;
        if (_inventorySplit.Width > 100)
        {
            var panel1Minimum = Math.Min(300, (_inventorySplit.Width - _inventorySplit.SplitterWidth) / 2);
            var panel2Minimum = Math.Min(260, (_inventorySplit.Width - _inventorySplit.SplitterWidth) / 2);
            _inventorySplit.Panel1MinSize = 0;
            _inventorySplit.Panel2MinSize = 0;
            var desired = (int)(_inventorySplit.Width * (narrow ? 0.55 : 0.62));
            var maximum = _inventorySplit.Width - panel2Minimum - _inventorySplit.SplitterWidth;
            _inventorySplit.SplitterDistance = Math.Clamp(desired, panel1Minimum, maximum);
            _inventorySplit.Panel1MinSize = panel1Minimum;
            _inventorySplit.Panel2MinSize = panel2Minimum;
        }
        _contentLayout.PerformLayout();
    }

    internal void RunLayoutSelfTest()
    {
        foreach (var size in new[] { new Size(860, 620), new Size(1024, 700), new Size(1280, 820), new Size(1600, 1000) })
        {
            ClientSize = size;
            ApplyResponsiveLayout();
            PerformLayout();
            if (_toolbar.Width <= 0 || _toolbar.Height <= 0 || _grid.Width <= 0 || _remoteGrid.Width <= 0 || _inventorySplit.Panel1.Width <= 0 || _inventorySplit.Panel2.Width <= 0 || _activity.Width <= 0 ||
                _copyrightFooter.Width <= 0 || _copyrightFooter.Height <= 0)
                throw new InvalidOperationException($"Responsive layout failed at {size.Width}x{size.Height}.");
        }
    }

    private static void ConfigureGrid(DataGridView grid, bool remoteTools)
    {
        grid.Dock = DockStyle.Fill; grid.AutoGenerateColumns = false; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = true; grid.RowHeadersVisible = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BorderStyle = BorderStyle.None; grid.BackgroundColor = Color.White; grid.GridColor = Color.FromArgb(226, 232, 240); grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), ForeColor = Slate, Font = new Font("Segoe UI Semibold", 9), Padding = new Padding(8, 0, 8, 0), SelectionBackColor = Color.FromArgb(248, 250, 252), SelectionForeColor = Slate };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Color.FromArgb(30, 41, 59), SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(12, 74, 110), Padding = new Padding(8, 5, 8, 5) };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(12, 74, 110), Padding = new Padding(8, 5, 8, 5) }; grid.RowTemplate.Height = 42;
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Name), DataPropertyName = nameof(UiEntry.Name), HeaderText = "PRODUCT", FillWeight = remoteTools ? 44 : 31 });
        if (remoteTools)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.RemoteDisposition), DataPropertyName = nameof(UiEntry.RemoteDisposition), HeaderText = "CLASSIFICATION", FillWeight = 25 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Decision), DataPropertyName = nameof(UiEntry.Decision), HeaderText = "POLICY", FillWeight = 22 });
        }
        else
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Category), DataPropertyName = nameof(UiEntry.Category), HeaderText = "CATEGORY", FillWeight = 19 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Brand), DataPropertyName = nameof(UiEntry.Brand), HeaderText = "BRAND", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Type), DataPropertyName = nameof(UiEntry.Type), HeaderText = "TYPE", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Risk), DataPropertyName = nameof(UiEntry.Risk), HeaderText = "RISK", FillWeight = 11 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Confidence), DataPropertyName = nameof(UiEntry.Confidence), HeaderText = "MATCH", FillWeight = 9 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Decision), DataPropertyName = nameof(UiEntry.Decision), HeaderText = "DECISION", FillWeight = 13 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(UiEntry.Reason), DataPropertyName = nameof(UiEntry.Reason), HeaderText = "POLICY REASON", FillWeight = 25 });
        }
    }

    private void LoadUiPreview()
    {
        static PlanItem PreviewItem(string id, string vendor, string name, RiskLevel risk, DecisionAction action, string reason, bool security = false)
        {
            var inventory = new InventoryItem($"preview-{id}", name, "1.0", vendor, PackageType.Exe, "Synthetic preview");
            var catalog = new CatalogEntry
            {
                Id = id,
                Vendor = vendor,
                Brand = "Any",
                ProductPattern = Regex.Escape(name),
                PackageType = PackageType.Exe,
                RiskLevel = risk,
                DetectionMethod = "Synthetic preview",
                AutomaticRemovalSupported = false,
                IsSecurityProduct = security,
                Notes = "Synthetic documentation preview entry."
            };
            return new PlanItem(inventory, catalog, new PolicyDecision(action, reason), 100, ["Synthetic documentation preview"]);
        }

        BindEntries(
        [
            PreviewItem("asus-giftbox", "ASUS", "ASUS Giftbox", RiskLevel.Safe, DecisionAction.Remove, "Cataloged optional software"),
            PreviewItem("trial-wildtangent", "WildTangent", "WildTangent Games", RiskLevel.Safe, DecisionAction.Remove, "Cataloged trialware"),
            PreviewItem("security-mcafee", "McAfee", "McAfee LiveSafe", RiskLevel.Caution, DecisionAction.AuditOnly, "Security removal requires explicit authorization", security: true),
            PreviewItem("remote-screenconnect", "ScreenConnect", "ScreenConnect Client", RiskLevel.ManualReview, DecisionAction.Skip, "Approved ConnectWise management platform"),
            PreviewItem("remote-anydesk", "AnyDesk", "AnyDesk", RiskLevel.ManualReview, DecisionAction.ManualReview, "Remote-tool removal was not explicitly enabled")
        ]);
        _summary.Text = "Review policy decisions";
        _resultCount.Text = "Synthetic UI preview - no hostname, hardware, account, or installed-software inventory";
        _scan.Enabled = false; _run.Enabled = false; _export.Enabled = false; _execute.Enabled = false;
    }

    private void BindEntries(IEnumerable<PlanItem> plan)
    {
        var allEntries = plan.Select(x => new UiEntry(x)).ToList();
        _remoteEntries = allEntries.Where(x => SoftwareClassification.IsRemoteManagementTool(x.Plan)).ToList();
        _entries = allEntries.Where(x => !SoftwareClassification.IsRemoteManagementTool(x.Plan)).ToList();
        _grid.DataSource = _entries; _remoteGrid.DataSource = _remoteEntries;
        _oemCount.Text = $"{_entries.Count} found";
        _remoteCount.Text = _remoteEntries.Count == 0 ? "None found" : $"{_remoteEntries.Count} found";
    }

    private async Task AuditAsync()
    {
        SetBusy(true); _hasExecuted = false;
        try
        {
            _config = PolicyConfiguration.Load();
            var policy = CurrentPolicy(); var id = Guid.NewGuid().ToString("N"); var directory = ReportDirectory(policy);
            var logPath = Path.Combine(directory, $"audit-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{id[..8]}.jsonl");
            Log($"Audit started. Profile={policy.Profile}; Mode={(policy.DryRun ? "Dry-run" : "Removal")}; SecurityRemoval={policy.AllowSecurityProductRemoval}; RemoteToolRemoval={policy.AllowRemoteManagementRemoval}");
            _report = await Task.Run(() => _engine.Audit(policy, id, logPath));
            _report.Results = _report.Before.Select(x => new ExecutionResult(x, ExecutionOutcome.Detected, null, x.Decision.Reason, false, DateTimeOffset.UtcNow)).ToList();
            _report.After = _report.Before; _report.AfterInventory = _report.FullInventory; _report.CompletedAt = DateTimeOffset.UtcNow;
            _report.Summary = BuildSummary(_report); _report.AuditLogSha256 = HashChainAuditLogger.Sha256File(logPath);
            (_jsonReportPath, _htmlReportPath) = ReportWriter.Write(_report, directory);
            BindEntries(_report.Before);
            var allEntries = _entries.Concat(_remoteEntries).ToList();
            _summary.Text = allEntries.Count == 0 ? "All clear" : "Review policy decisions";
            _resultCount.Text = $"{_entries.Count} OEM/optional and {_remoteEntries.Count} remote-tool match(es) across {_report.FullInventory.Count} inventoried item(s) - {_report.Device.Manufacturer} {_report.Device.Model}";
            Log($"Audit complete. Matches={allEntries.Count}; RemoteTools={_remoteEntries.Count}; Inventory={_report.FullInventory.Count}; Admin={_report.Device.IsAdministrator}; RebootPending={_report.Device.RebootPending}");
            Log($"Reports: {_jsonReportPath} | {_htmlReportPath}"); _export.Enabled = true;
        }
        catch (Exception ex) { Log("AUDIT FAILED: " + ex.Message); MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { SetBusy(false); }
    }

    private async Task RunSelectedAsync()
    {
        if (_report is null) return;
        var selected = _grid.SelectedRows.Cast<DataGridViewRow>().Concat(_remoteGrid.SelectedRows.Cast<DataGridViewRow>())
            .Select(r => r.DataBoundItem as UiEntry).Where(x => x?.Plan.Decision.Action == DecisionAction.Remove)
            .Cast<UiEntry>().Select(x => x.Plan).DistinctBy(x => x.Inventory.Id + "|" + x.Catalog.Id).ToList();
        if (selected.Count == 0) { MessageBox.Show("Select one or more entries whose policy decision is Remove.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var policy = CurrentPolicy();
        if (!policy.DryRun && !policy.Force)
        {
            var securityCount = selected.Count(x => x.Catalog.IsSecurityProduct);
            var remoteCount = selected.Count(SoftwareClassification.IsRemoteManagementTool);
            var warning = $"This will execute validated uninstallers for {selected.Count} selected product(s)."
                + (securityCount > 0 ? $"{Environment.NewLine}{securityCount} endpoint-protection product(s) are included." : "")
                + (remoteCount > 0 ? $"{Environment.NewLine}{remoteCount} remote-management tool(s) are included." : "")
                + $"{Environment.NewLine}{Environment.NewLine}Continue?";
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
        AllowRemoteManagementRemoval = _allowRemote.Checked,
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

    private void ToggleActivity()
    {
        _activityVisible = !_activityVisible;
        _toggleLog.Text = _activityVisible ? "Hide activity" : "Show activity";
        ApplyResponsiveLayout();
    }

    private void SetBusy(bool busy) { UseWaitCursor = busy; _progress.Visible = busy; _scan.Enabled = !busy; _profile.Enabled = !busy; _execute.Enabled = !busy; _allowSecurity.Enabled = !busy; _allowRemote.Enabled = !busy; if (busy) _run.Enabled = false; else UpdateRunButton(); }
    private void UpdateRunButton()
    {
        var has = _grid.SelectedRows.Cast<DataGridViewRow>().Concat(_remoteGrid.SelectedRows.Cast<DataGridViewRow>())
            .Any(r => r.DataBoundItem is UiEntry x && x.Plan.Decision.Action == DecisionAction.Remove);
        _run.Enabled = !UseWaitCursor && !_hasExecuted && has;
    }
    private void FormatCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid || e.ColumnIndex < 0) return;
        var property = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (property == nameof(UiEntry.Risk) && e.Value is string risk) e.CellStyle!.ForeColor = risk == "Low risk" ? Color.FromArgb(5, 150, 105) : risk == "Review first" ? Color.FromArgb(217, 119, 6) : Red;
        if (property == nameof(UiEntry.Decision) && e.Value is string decision) e.CellStyle!.ForeColor = decision == "Can uninstall" ? Red : Slate;
        if (property == nameof(UiEntry.RemoteDisposition) && e.Value is string disposition)
        {
            e.CellStyle!.Font = grid.ColumnHeadersDefaultCellStyle.Font;
            e.CellStyle.ForeColor = disposition == "Approved" ? Color.FromArgb(5, 150, 105) : Color.FromArgb(217, 119, 6);
        }
        if (property == nameof(UiEntry.Category) && e.Value is string category)
        {
            e.CellStyle!.Font = grid.ColumnHeadersDefaultCellStyle.Font;
            e.CellStyle.ForeColor = category switch
            {
                "Antivirus / Security" => Color.FromArgb(185, 28, 28),
                "OEM Control Panel" => Color.FromArgb(109, 40, 217),
                "Hardware / Recovery" => Color.FromArgb(180, 83, 9),
                "Bloatware" => Color.FromArgb(194, 65, 12),
                "Trialware" => Color.FromArgb(161, 98, 7),
                "Consumer App" => Color.FromArgb(8, 145, 178),
                "OEM Support / Updates" => Color.FromArgb(29, 78, 216),
                _ => Slate
            };
        }
    }

    private void ShowTechnicalName(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Rows[e.RowIndex].DataBoundItem is not UiEntry entry) return;
        if (grid.Columns[e.ColumnIndex].Name == nameof(UiEntry.Name))
            e.ToolTipText = $"Technical name: {entry.Plan.Inventory.Name}{Environment.NewLine}Identifier: {entry.Plan.Inventory.Id}";
        else if (grid.Columns[e.ColumnIndex].Name == nameof(UiEntry.Category))
            e.ToolTipText = FriendlyDisplay.CategoryDescription(entry.Category);
        else if (grid.Columns[e.ColumnIndex].Name == nameof(UiEntry.RemoteDisposition))
            e.ToolTipText = FriendlyDisplay.CategoryDescription(entry.Category);
        else if (grid.Columns[e.ColumnIndex].Name == nameof(UiEntry.Decision))
            e.ToolTipText = entry.Reason;
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
            File.WriteAllText(path, $"TimestampUtc: {DateTimeOffset.UtcNow:O}{Environment.NewLine}Stage: {stage}{Environment.NewLine}Hostname: {Environment.MachineName}{Environment.NewLine}User: {Environment.UserName}{Environment.NewLine}OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}{Environment.NewLine}Process: {Environment.ProcessPath}{Environment.NewLine}Version: {ProductInfo.Version}{Environment.NewLine}{Environment.NewLine}{exception}");
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

internal sealed class BorderedPanel : Panel
{
    public Color BorderColor { get; set; } = Color.LightGray;
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(BorderColor); e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
}
