using PreloadedAVRemover.Installer;
using System.Diagnostics;
using System.Reflection;

namespace PreloadedAVRemover.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var automated = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase) || args.Contains("--verify-payloads", StringComparer.OrdinalIgnoreCase);
        try
        {
            ApplicationConfiguration.Initialize();
            using var form = new SetupForm();
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) { form.CreateControl(); form.RunLayoutSelfTest(); return 0; }
            if (args.Contains("--verify-payloads", StringComparer.OrdinalIgnoreCase)) { form.VerifyPayloads(); return 0; }
            Application.Run(form);
            return 0;
        }
        catch (Exception ex)
        {
            if (!automated) MessageBox.Show(ex.Message, "OEM Endpoint Cleanup Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

internal sealed class SetupForm : Form
{
    private static readonly Color Navy = Color.FromArgb(8, 42, 76);
    private static readonly Color Blue = Color.FromArgb(0, 155, 239);
    private static readonly Color Slate = Color.FromArgb(71, 85, 105);
    private readonly RadioButton _everyone = new() { Text = "Everyone on this computer", AutoSize = true, Font = new Font("Segoe UI Semibold", 11) };
    private readonly RadioButton _currentUser = new() { Text = "Just me", AutoSize = true, Font = new Font("Segoe UI Semibold", 11), Checked = true };
    private readonly RadioButton _portable = new() { Text = "Portable", AutoSize = true, Font = new Font("Segoe UI Semibold", 11) };
    private readonly Button _continue = new() { Text = "Continue", Width = 122, Height = 38 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Slate, Text = "Choose how you want to use OEM Endpoint Cleanup." };
    private readonly IReadOnlyDictionary<string, string> _metadata;
    private PictureBox _logo = null!;
    private TableLayoutPanel _body = null!;
    private readonly List<Label> _choiceDescriptions = [];

    public SetupForm()
    {
        _metadata = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.Ordinal);
        Text = "OEM Endpoint Cleanup Setup";
        ClientSize = new Size(780, 710);
        MinimumSize = new Size(720, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 247, 250);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;

        var header = new Panel { Dock = DockStyle.Top, Height = 132, BackColor = Color.White, Padding = new Padding(30, 20, 30, 15) };
        _logo = new PictureBox { Image = LoadLogo(), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 20, 0) };
        var title = new Label { Text = "OEM Endpoint Cleanup", AutoSize = true, Font = new Font("Segoe UI Semibold", 13), ForeColor = Navy, Margin = new Padding(0, 5, 0, 0) };
        var subtitle = new Label { Text = "Secure audit and removal by IT Health Tech LLC", AutoSize = true, ForeColor = Slate, Margin = new Padding(0, 8, 0, 0) };
        var titleStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
        titleStack.Controls.AddRange([title, subtitle]);
        var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        headerLayout.Controls.Add(_logo, 0, 0); headerLayout.Controls.Add(titleStack, 1, 0); header.Controls.Add(headerLayout);

        _body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(34, 24, 34, 18), RowCount = 5, ColumnCount = 1 };
        _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _body.Controls.Add(new Label { Text = "INSTALLATION MODE", AutoSize = true, Font = new Font("Segoe UI Semibold", 9), ForeColor = Navy }, 0, 0);

        var choices = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(0, 12, 0, 10) };
        choices.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f)); choices.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f)); choices.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334f));
        choices.Controls.Add(CreateChoice(_everyone, "Installs in Program Files for every Windows user. Requests administrator approval."), 0, 0);
        choices.Controls.Add(CreateChoice(_currentUser, "Installs only for your Windows account. Other users are unchanged."), 0, 1);
        choices.Controls.Add(CreateChoice(_portable, "Extracts to a folder you choose. No installer registration or shortcuts."), 0, 2);
        _body.Controls.Add(choices, 0, 1);
        _body.Controls.Add(_status, 0, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0) };
        var cancel = new Button { Text = "Cancel", Width = 100, Height = 38, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
        cancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _continue.BackColor = Blue; _continue.ForeColor = Color.White; _continue.FlatStyle = FlatStyle.Flat; _continue.FlatAppearance.BorderSize = 0;
        buttons.Controls.AddRange([_continue, cancel]); _body.Controls.Add(buttons, 0, 3);
        _body.Controls.Add(new Label { Text = "Copyright © 2026 IT Health Tech LLC  •  GPL-3.0-only", AutoSize = true, ForeColor = Slate, Padding = new Padding(0, 12, 0, 0) }, 0, 4);
        Controls.Add(_body); Controls.Add(header);
        AcceptButton = _continue; CancelButton = cancel;
        _continue.Click += async (_, _) => await ContinueAsync();
    }

    internal void RunLayoutSelfTest()
    {
        foreach (var size in new[] { new Size(720, 680), new Size(780, 710), new Size(1024, 768) })
        {
            ClientSize = size;
            PerformLayout();
            _body.PerformLayout();
            if (_logo.Width <= 0 || _logo.Height <= 0 || _continue.Width <= 0 || _continue.Height <= 0 ||
                _everyone.Width <= 0 || _currentUser.Width <= 0 || _portable.Width <= 0 || _status.Width <= 0)
                throw new InvalidOperationException($"Setup layout failed at {size.Width}x{size.Height}.");
            foreach (var detail in _choiceDescriptions)
                if (detail.Bottom > detail.Parent!.ClientSize.Height - detail.Parent.Padding.Bottom)
                    throw new InvalidOperationException($"Setup option text is clipped at {size.Width}x{size.Height}.");
        }
    }

    internal void VerifyPayloads()
    {
        _ = ValidatedPayload("InstallerMsiFileName", "InstallerMsiSha256");
        _ = ValidatedPayload("PortableZipFileName", "PortableZipSha256");
    }

    private Control CreateChoice(RadioButton radio, string description)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 5, 0, 5), Padding = new Padding(18, 13, 18, 10), Cursor = Cursors.Hand };
        var detail = new Label { Text = description, AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Slate, Location = new Point(39, 41) };
        _choiceDescriptions.Add(detail);
        radio.Location = new Point(16, 12);
        panel.Controls.Add(detail); panel.Controls.Add(radio);
        void SelectOption(object? _, EventArgs __) => radio.Checked = true;
        panel.Click += SelectOption; detail.Click += SelectOption;
        return panel;
    }

    private async Task ContinueAsync()
    {
        _continue.Enabled = false;
        try
        {
            var mode = _everyone.Checked ? InstallMode.Everyone : _portable.Checked ? InstallMode.Portable : InstallMode.CurrentUser;
            if (mode == InstallMode.Portable) await ExtractPortableAsync(); else await InstallMsiAsync(mode);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _status.Text = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            _status.Text = "Setup did not complete.";
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _continue.Enabled = true; }
    }

    private async Task InstallMsiAsync(InstallMode mode)
    {
        var msiPath = ValidatedPayload("InstallerMsiFileName", "InstallerMsiSha256");
        _status.Text = "Starting Windows Installer…";
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
            UseShellExecute = mode == InstallMode.Everyone,
            Verb = mode == InstallMode.Everyone ? "runas" : string.Empty
        };
        foreach (var argument in InstallerOperations.BuildMsiArguments(mode, msiPath)) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows Installer could not be started.");
        await process.WaitForExitAsync();
        if (process.ExitCode is not (0 or 3010 or 1641)) throw new InvalidOperationException($"Windows Installer exited with code {process.ExitCode}.");
        _status.Text = process.ExitCode == 0 ? "Installation completed." : "Installation completed; Windows requires a restart.";
        MessageBox.Show(_status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ExtractPortableAsync()
    {
        var zipPath = ValidatedPayload("PortableZipFileName", "PortableZipSha256");
        using var picker = new FolderBrowserDialog { Description = "Choose where to create the portable folder", UseDescriptionForTitle = true, ShowNewFolderButton = true };
        if (picker.ShowDialog(this) != DialogResult.OK) { _status.Text = "Portable extraction cancelled."; return; }
        var destination = InstallerOperations.GetPortableDestination(picker.SelectedPath);
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            throw new IOException($"The destination already contains files:{Environment.NewLine}{destination}{Environment.NewLine}{Environment.NewLine}Choose another folder or remove the existing portable folder first.");
        _status.Text = "Extracting portable files…";
        await Task.Run(() => InstallerOperations.ExtractZipSafely(zipPath, destination));
        _status.Text = "Portable copy created.";
        if (MessageBox.Show($"Portable copy created at:{Environment.NewLine}{destination}{Environment.NewLine}{Environment.NewLine}Open the folder now?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            Process.Start(new ProcessStartInfo("explorer.exe", destination) { UseShellExecute = true });
    }

    private string ValidatedPayload(string fileNameKey, string hashKey)
    {
        if (!_metadata.TryGetValue(fileNameKey, out var fileName) || !_metadata.TryGetValue(hashKey, out var hash))
            throw new InvalidOperationException("Installer payload metadata is missing. Download the complete installer package again.");
        var path = InstallerOperations.ResolvePayloadPath(AppContext.BaseDirectory, fileName);
        if (!InstallerOperations.HasExpectedSha256(path, hash))
            throw new InvalidDataException($"Security validation failed for {fileName}. The file is missing or its SHA-256 does not match this launcher.");
        return path;
    }

    private static Image LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ItHealthTechLogo")
            ?? throw new InvalidOperationException("Installer logo resource is missing.");
        return new Bitmap(stream);
    }
}
