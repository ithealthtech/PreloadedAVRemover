using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PreloadedAVRemover.Core;

public sealed record ValidatedCommand(string FileName, IReadOnlyList<string> Arguments, bool Interactive, string Source, int TimeoutSeconds = 900);
public sealed record ValidationResult(bool IsValid, string Reason, ValidatedCommand? Command = null);

public static class CommandValidator
{
    private static readonly HashSet<string> DisallowedRegistryExecutables = new(StringComparer.OrdinalIgnoreCase)
        { "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "rundll32.exe", "reg.exe", "sc.exe", "schtasks.exe", "mshta.exe" };

    public static ValidationResult Validate(PlanItem plan, int timeoutSeconds = 900)
    {
        if (plan.Decision.Action != DecisionAction.Remove) return new(false, "Policy did not authorize removal");
        timeoutSeconds = Math.Clamp(timeoutSeconds, 30, 3600);
        return plan.Inventory.PackageType switch
        {
            PackageType.Msi => ValidateMsi(plan.Inventory, timeoutSeconds),
            PackageType.Exe => ValidateRegistryExe(plan.Inventory, timeoutSeconds),
            PackageType.Appx => ValidateAppx(plan.Inventory, timeoutSeconds),
            PackageType.Winget => ValidateWinget(plan.Inventory, timeoutSeconds),
            _ => new(false, $"Automatic {plan.Inventory.PackageType} removal requires an explicitly implemented catalog handler")
        };
    }

    private static ValidationResult ValidateMsi(InventoryItem item, int timeoutSeconds)
    {
        var source = item.QuietUninstallString ?? item.UninstallString ?? item.Id;
        var match = Regex.Match(source, @"\{[0-9A-Fa-f-]{36}\}");
        if (!match.Success) return new(false, "MSI product code is missing or malformed");
        return new(true, "Validated MSI product code", new(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), ["/x", match.Value, "/qn", "/norestart"], false, "Cataloged MSI", timeoutSeconds));
    }

    private static ValidationResult ValidateRegistryExe(InventoryItem item, int timeoutSeconds)
    {
        var raw = item.QuietUninstallString ?? item.UninstallString;
        if (string.IsNullOrWhiteSpace(raw) || raw.IndexOfAny(['\r', '\n', '\0']) >= 0) return new(false, "Uninstall command is missing or contains control characters");
        if (Regex.IsMatch(raw, @"%[^%]+%")) return new(false, "Environment-expanded executable paths are not permitted");
        var normalized = raw.Trim();
        if (!normalized.StartsWith('"'))
        {
            var exeEnd = normalized.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeEnd >= 0)
            {
                exeEnd += 4;
                var candidate = normalized[..exeEnd];
                if (File.Exists(candidate)) normalized = $"\"{candidate}\"{normalized[exeEnd..]}";
            }
        }
        var parts = SafeCommandLine.Split(normalized);
        if (parts.Length == 0) return new(false, "Uninstall command could not be parsed");
        var executable = parts[0].Trim();
        if (!Path.IsPathFullyQualified(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return new(false, "Registry executable must be an absolute .exe path");
        if (executable.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase)) return new(false, "Network-hosted uninstallers are not permitted");
        if (DisallowedRegistryExecutables.Contains(Path.GetFileName(executable))) return new(false, "Registry command invokes a disallowed script or shell host");
        if (!File.Exists(executable)) return new(false, "Registered uninstaller does not exist");
        if (parts.Skip(1).Any(a => a.IndexOfAny(['\r', '\n', '\0']) >= 0)) return new(false, "Uninstall arguments contain control characters");
        return new(true, "Validated direct executable and argument vector", new(executable, parts.Skip(1).ToArray(), item.QuietUninstallString is null, "Registered uninstall command", timeoutSeconds));
    }

    private static ValidationResult ValidateAppx(InventoryItem item, int timeoutSeconds)
    {
        var package = item.PackageFullName;
        if (string.IsNullOrWhiteSpace(package) || !Regex.IsMatch(package, @"^[A-Za-z0-9._-]+$")) return new(false, "AppX package full name is malformed");
        var powerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return new(true, "Validated fixed AppX handler", new(powerShell, ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Remove-AppxPackage -AllUsers -Package $args[0] -ErrorAction Stop", package], false, "Fixed AppX handler", timeoutSeconds));
    }

    private static ValidationResult ValidateWinget(InventoryItem item, int timeoutSeconds)
    {
        if (!Regex.IsMatch(item.Id, @"^[A-Za-z0-9._-]+$")) return new(false, "winget package ID is malformed");
        var winget = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p, "winget.exe")).FirstOrDefault(File.Exists);
        return winget is null ? new(false, "winget is unavailable") : new(true, "Validated fixed winget handler", new(winget, ["uninstall", "--id", item.Id, "--exact", "--silent", "--accept-source-agreements", "--disable-interactivity"], false, "Fixed winget handler", timeoutSeconds));
    }
}

public interface IProcessRunner { Task<int> RunAsync(ValidatedCommand command, CancellationToken cancellationToken = default); }
public sealed class ProcessTimeoutException(string message) : TimeoutException(message);
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(ValidatedCommand command, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(command.FileName) { UseShellExecute = false, CreateNoWindow = !command.Interactive };
        foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start validated uninstaller.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(command.TimeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } catch { }
            throw new ProcessTimeoutException($"Validated uninstaller exceeded the {command.TimeoutSeconds}-second timeout.");
        }
        return process.ExitCode;
    }
}

internal static class SafeCommandLine
{
    public static string[] Split(string command)
    {
        var pointer = CommandLineToArgvW(command, out var count);
        if (pointer == IntPtr.Zero) return [];
        try { var result = new string[count]; for (var i = 0; i < count; i++) result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size)) ?? ""; return result; }
        finally { LocalFree(pointer); }
    }
    [DllImport("shell32.dll", SetLastError = true)] private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
