using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PreloadedAVRemover;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            using var form = new MainForm();
            form.CreateControl();
            return;
        }
        Application.Run(new MainForm());
    }
}

internal sealed record AppEntry(string Name, string Version, string Publisher, string? RemovalCommand, bool IsSilent, string RegistryPath)
{
    public string Status => string.IsNullOrWhiteSpace(RemovalCommand)
        ? "No removal command available"
        : IsSilent ? "Ready - silent" : "Ready - confirmation required";
}

internal sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(15, 23, 42);
    private static readonly Color Slate = Color.FromArgb(71, 85, 105);
    private static readonly Color Canvas = Color.FromArgb(241, 245, 249);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);

    private static readonly string[] AvPatterns =
    [
        @"(^|\W)McAfee(\W|$)", @"(^|\W)Norton(\W|$)", @"(^|\W)Symantec(\W|$)",
        @"(^|\W)Trend Micro(\W|$)", @"(^|\W)Avast(\W|$)", @"(^|\W)AVG(\W|$)",
        @"(^|\W)Kaspersky(\W|$)", @"(^|\W)ESET(\W|$)", @"(^|\W)Bitdefender(\W|$)",
        @"(^|\W)Panda(\W|$)", @"(^|\W)Webroot(\W|$)", @"(^|\W)F-Secure(\W|$)", @"(^|\W)Malwarebytes(\W|$)"
    ];
    private static readonly string[] OptionalNames =
        ["McAfee WebAdvisor", "Norton Security Scan", "Norton Private Browser", "Avast Secure Browser", "AVG Secure Browser"];

    private readonly CheckBox _includeExtras = new() { Text = "Include browser and security extras", AutoSize = true };
    private readonly Button _scan = new() { Text = "Scan again" };
    private readonly Button _remove = new() { Text = "Remove selected", Enabled = false };
    private readonly Label _summary = new() { AutoSize = true, Text = "Ready to scan", Font = new Font("Segoe UI Semibold", 11), ForeColor = Color.FromArgb(30, 41, 59) };
    private readonly Label _resultCount = new() { AutoSize = true, Text = "0 products detected", ForeColor = Slate, Margin = new Padding(0, 4, 0, 0) };
    private readonly DataGridView _grid = new();
    private readonly TextBox _activity = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None, BackColor = Navy, ForeColor = Color.FromArgb(203, 213, 225) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 3, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
    private List<AppEntry> _entries = [];

    public MainForm()
    {
        Text = "Preloaded AV Remover 1.2.0";
        MinimumSize = new Size(920, 660);
        Size = new Size(1100, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Canvas;
        Font = new Font("Segoe UI", 9.5f);

        StyleButton(_scan, Color.White, Color.FromArgb(30, 41, 59), Color.FromArgb(203, 213, 225), 112);
        StyleButton(_remove, Red, Color.White, Red, 188);
        _includeExtras.ForeColor = Slate;
        _includeExtras.Margin = new Padding(8, 8, 0, 0);

        var header = BuildHeader();
        var contentHeader = BuildContentHeader();
        ConfigureGrid();

        var gridCard = new BorderedPanel { Dock = DockStyle.Fill, BackColor = Color.White, BorderColor = Color.FromArgb(226, 232, 240), Margin = new Padding(0, 10, 0, 16), Padding = new Padding(1) };
        gridCard.Controls.Add(_grid);

        var activityTitle = new Label { Text = "ACTIVITY LOG", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(148, 163, 184), BackColor = Navy, Padding = new Padding(13, 11, 0, 0) };
        var activityCard = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(14, 0, 14, 14), Margin = new Padding(0) };
        activityCard.Controls.Add(_activity);
        activityCard.Controls.Add(activityTitle);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30, 24, 30, 26), RowCount = 3, ColumnCount = 1, BackColor = Canvas };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
        content.Controls.Add(contentHeader, 0, 0);
        content.Controls.Add(gridCard, 0, 1);
        content.Controls.Add(activityCard, 0, 2);
        Controls.Add(content);
        Controls.Add(header);

        _scan.Click += async (_, _) => await ScanAsync();
        _remove.Click += async (_, _) => await RemoveSelectedAsync();
        _grid.SelectionChanged += (_, _) => UpdateRemoveButton();
        _grid.CellFormatting += FormatStatusCell;
        Shown += async (_, _) => await ScanAsync();
    }

    private Control BuildHeader()
    {
        var mark = new ShieldMark { Size = new Size(58, 58), Margin = new Padding(0, 2, 18, 0) };
        var heading = new Label { Text = "Preloaded AV Remover", Font = new Font("Segoe UI Semibold", 22), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0) };
        var version = new Label { Text = "VERSION 1.2.0", Font = new Font("Segoe UI Semibold", 8), ForeColor = Color.FromArgb(94, 234, 212), AutoSize = true, Margin = new Padding(2, 7, 0, 0) };
        var detail = new Label { Text = "Clean out OEM security trials and restore a simpler Windows setup.", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(203, 213, 225), AutoSize = true, Margin = new Padding(2, 7, 0, 0) };
        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
        text.Controls.AddRange([heading, version, detail]);
        var inner = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(30, 23, 30, 18) };
        inner.Controls.AddRange([mark, text]);
        var header = new GradientPanel { Dock = DockStyle.Top, Height = 132 };
        header.Controls.Add(inner);
        header.Controls.Add(_progress);
        return header;
    }

    private Control BuildContentHeader()
    {
        var status = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        status.Controls.AddRange([_summary, _resultCount]);
        var defender = new Label { Text = "DEFENDER UNTOUCHED", AutoSize = true, BackColor = Color.FromArgb(220, 252, 231), ForeColor = Color.FromArgb(22, 101, 52), Font = new Font("Segoe UI Semibold", 8), Padding = new Padding(9, 7, 9, 7), Margin = new Padding(16, 3, 0, 0) };
        var tools = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        tools.Controls.AddRange([_scan, _remove, _includeExtras, defender]);
        var result = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        result.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        result.Controls.Add(status, 0, 0);
        result.Controls.Add(tools, 1, 0);
        return result;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = Color.White;
        _grid.GridColor = Color.FromArgb(226, 232, 240);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.ColumnHeadersHeight = 42;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), ForeColor = Slate, Font = new Font("Segoe UI Semibold", 9), Padding = new Padding(8, 0, 8, 0), SelectionBackColor = Color.FromArgb(248, 250, 252), SelectionForeColor = Slate };
        _grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Color.FromArgb(30, 41, 59), SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(12, 74, 110), Padding = new Padding(8, 5, 8, 5) };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(12, 74, 110), Padding = new Padding(8, 5, 8, 5) };
        _grid.RowTemplate.Height = 42;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AppEntry.Name), HeaderText = "PRODUCT", FillWeight = 33 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AppEntry.Version), HeaderText = "VERSION", FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AppEntry.Publisher), HeaderText = "PUBLISHER", FillWeight = 23 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AppEntry.Status), HeaderText = "REMOVAL STATUS", FillWeight = 28 });
    }

    private void FormatStatusCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].DataPropertyName != nameof(AppEntry.Status) || e.Value is not string status) return;
        e.CellStyle!.ForeColor = status.Contains("silent", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(5, 150, 105)
            : status.Contains("confirmation", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(217, 119, 6)
            : Red;
        e.CellStyle.Font = new Font("Segoe UI Semibold", 9);
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor, Color borderColor, int width)
    {
        button.AutoSize = false;
        button.Size = new Size(width, 36);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = borderColor;
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.Font = new Font("Segoe UI Semibold", 9);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(0, 0, 10, 0);
    }

    private void Log(string message) => _activity.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");

    private async Task ScanAsync()
    {
        SetBusy(true);
        _summary.Text = "Scanning installed applications...";
        Log("Scanning installed applications...");
        _entries = await Task.Run(() => FindApps(_includeExtras.Checked));
        _grid.DataSource = _entries;
        _summary.Text = _entries.Count == 0 ? "All clear" : "Review detected products";
        _resultCount.Text = _entries.Count == 0 ? "No supported third-party AV products detected" : $"{_entries.Count} product(s) detected - select rows to remove";
        Log(_entries.Count == 0 ? "Verified: no supported third-party AV products were detected." : $"Detected {_entries.Count} product(s). Select one or more rows to remove.");
        SetBusy(false);
    }

    private async Task RemoveSelectedAsync()
    {
        var selected = SelectedRemovableEntries();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select one or more products with a removal command.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var names = string.Join(Environment.NewLine, selected.Select(e => "- " + e.Name));
        var interactiveCount = selected.Count(e => !e.IsSilent);
        var note = interactiveCount > 0 ? $"{Environment.NewLine}{Environment.NewLine}{interactiveCount} product(s) require their normal uninstall window. Follow its prompts to finish." : string.Empty;
        if (MessageBox.Show($"Uninstall these products?{Environment.NewLine}{Environment.NewLine}{names}{note}", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        SetBusy(true);
        foreach (var entry in selected)
        {
            Log($"Removing {entry.Name} ({(entry.IsSilent ? "silent" : "interactive")})...");
            var exitCode = await Task.Run(() => RunCommand(entry.RemovalCommand!));
            Log(exitCode is 0 or 3010 or 1641 ? $"Removed {entry.Name} (exit code {exitCode})." : $"Could not remove {entry.Name} (exit code {exitCode}).");
        }
        await ScanAsync();
        var remaining = _entries.Select(e => e.Name).ToArray();
        if (remaining.Length == 0)
            MessageBox.Show("Verified: no supported third-party AV products remain.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show("Still detected: " + string.Join(", ", remaining), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        SetBusy(false);
    }

    private List<AppEntry> SelectedRemovableEntries() => _grid.SelectedRows.Cast<DataGridViewRow>()
        .Select(r => r.DataBoundItem as AppEntry).Where(e => e?.RemovalCommand is not null).Cast<AppEntry>().ToList();

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _progress.Visible = busy;
        _scan.Enabled = !busy;
        if (busy) _remove.Enabled = false; else UpdateRemoveButton();
    }

    private void UpdateRemoveButton()
    {
        var selected = SelectedRemovableEntries();
        _remove.Enabled = !UseWaitCursor && selected.Count > 0;
        _remove.Text = selected.Any(e => !e.IsSilent) ? "Remove selected (prompts)" : "Remove selected";
    }

    private static int RunCommand(string command)
    {
        var expanded = Environment.ExpandEnvironmentVariables(command).Trim();
        if (!expanded.StartsWith('"'))
        {
            var exeEnd = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeEnd >= 0)
            {
                exeEnd += 4;
                var candidate = expanded[..exeEnd];
                if (File.Exists(candidate)) expanded = $"\"{candidate}\"{expanded[exeEnd..]}";
            }
        }
        var parts = SplitCommandLine(expanded);
        if (parts.Length == 0) return -1;
        var startInfo = new ProcessStartInfo(parts[0]) { UseShellExecute = false };
        foreach (var argument in parts.Skip(1)) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo);
        process!.WaitForExit();
        return process.ExitCode;
    }

    private static string[] SplitCommandLine(string command)
    {
        var pointer = CommandLineToArgvW(command, out var count);
        if (pointer == IntPtr.Zero) return [];
        try
        {
            var result = new string[count];
            for (var index = 0; index < count; index++)
                result[index] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size)) ?? string.Empty;
            return result;
        }
        finally { LocalFree(pointer); }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int argumentCount);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    private static List<AppEntry> FindApps(bool includeExtras)
    {
        var results = new List<AppEntry>();
        foreach (var (hive, view, path) in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
        })
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(path);
            if (uninstall is null) continue;
            foreach (var subName in uninstall.GetSubKeyNames())
            {
                using var key = uninstall.OpenSubKey(subName);
                if (key is null) continue;
                var name = key.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name) || !IsTarget(name, includeExtras)) continue;
                var uninstallString = key.GetValue("UninstallString") as string;
                var quiet = key.GetValue("QuietUninstallString") as string;
                if (string.IsNullOrWhiteSpace(quiet) && (key.GetValue("WindowsInstaller")?.ToString() == "1" || MsiCode(uninstallString) is not null))
                {
                    var code = MsiCode(uninstallString);
                    if (code is not null) quiet = $"msiexec.exe /x {code} /qn /norestart";
                }
                var isSilent = !string.IsNullOrWhiteSpace(quiet);
                results.Add(new AppEntry(name, key.GetValue("DisplayVersion") as string ?? "-", key.GetValue("Publisher") as string ?? "-", isSilent ? quiet : uninstallString, isSilent, $"{hive}\\{path}\\{subName}"));
            }
        }
        return results.GroupBy(e => e.RegistryPath).Select(g => g.First()).OrderBy(e => e.Name).ToList();
    }

    private static bool IsTarget(string name, bool includeExtras) => AvPatterns.Any(p => Regex.IsMatch(name, p, RegexOptions.IgnoreCase)) || (includeExtras && OptionalNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)));
    private static string? MsiCode(string? value) => value is not null && Regex.Match(value, @"\{[0-9A-Fa-f-]{36}\}").Value is { Length: > 0 } code ? code : null;
}

internal sealed class GradientPanel : Panel
{
    public GradientPanel() => DoubleBuffered = true;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(15, 23, 42), Color.FromArgb(17, 94, 89), 0f);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

internal sealed class ShieldMark : Control
{
    public ShieldMark()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var glow = new SolidBrush(Color.FromArgb(42, 255, 255, 255));
        e.Graphics.FillEllipse(glow, 0, 0, Width - 1, Height - 1);
        using var pen = new Pen(Color.FromArgb(94, 234, 212), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var shield = new[] { new Point(Width / 2, 11), new Point(43, 17), new Point(40, 37), new Point(Width / 2, 47), new Point(16, 37), new Point(13, 17) };
        e.Graphics.DrawPolygon(pen, shield);
        e.Graphics.DrawLines(pen, new Point[] { new(20, 28), new(26, 34), new(37, 22) });
    }
}

internal sealed class BorderedPanel : Panel
{
    public Color BorderColor { get; set; } = Color.LightGray;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
