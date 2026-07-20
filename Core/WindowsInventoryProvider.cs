using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using System.Text.Json;

namespace PreloadedAVRemover.Core;

public sealed class WindowsInventoryProvider : IInventoryProvider
{
    private static readonly string[] OemMarkers = ["Dell", "HP", "Hewlett", "ASUS", "Acer", "Lenovo", "MSI", "Micro-Star", "Samsung", "Toshiba", "Dynabook", "Microsoft", "Surface", "LG", "Gigabyte", "Razer", "Alienware", "Fujitsu", "McAfee", "Norton", "Symantec", "Trend Micro", "Avast", "AVG", "Kaspersky", "ESET", "Bitdefender", "Panda", "Webroot", "F-Secure", "Malwarebytes"];

    public DeviceIdentity GetDeviceIdentity()
    {
        var manufacturer = RegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer") ?? "Unknown";
        var model = RegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName") ?? "Unknown";
        var bios = RegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVersion") ?? "Unknown";
        var serial = "Unavailable";
        TryWmi("SELECT Manufacturer, Model FROM Win32_ComputerSystem", o => { manufacturer = Text(o["Manufacturer"], manufacturer); model = Text(o["Model"], model); });
        TryWmi("SELECT SMBIOSBIOSVersion, SerialNumber FROM Win32_BIOS", o => { bios = Text(o["SMBIOSBIOSVersion"], bios); serial = Text(o["SerialNumber"], serial); });
        var identity = WindowsIdentity.GetCurrent();
        var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        return new DeviceIdentity(Environment.MachineName, manufacturer, model, bios, serial,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription, Environment.OSVersion.VersionString,
            identity.Name ?? Environment.UserName, admin, IsRebootPending(), GetSecurityProducts());
    }

    public IReadOnlyList<InventoryItem> GetInventory()
    {
        var items = new List<InventoryItem>();
        AddRegistryApps(items, RegistryHive.LocalMachine, RegistryView.Registry64);
        AddRegistryApps(items, RegistryHive.LocalMachine, RegistryView.Registry32);
        AddRegistryApps(items, RegistryHive.CurrentUser, RegistryView.Default);
        AddAppxPackages(items);
        AddOemServices(items);
        AddOemScheduledTasks(items);
        return items.GroupBy(x => $"{x.PackageType}|{x.Id}|{x.RegistryPath}", StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(x => x.Name).ToList();
    }

    private static void AddRegistryApps(List<InventoryItem> items, RegistryHive hive, RegistryView view)
    {
        const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(path);
            if (root is null) return;
            foreach (var subName in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(subName);
                var name = key?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                var publisher = key!.GetValue("Publisher") as string ?? "Unknown";
                var uninstall = key.GetValue("UninstallString") as string;
                var quiet = key.GetValue("QuietUninstallString") as string;
                var isMsi = Convert.ToString(key.GetValue("WindowsInstaller")) == "1" || Guid.TryParse(subName.Trim('{', '}'), out _);
                items.Add(new InventoryItem(subName, name, key.GetValue("DisplayVersion") as string ?? "Unknown", publisher,
                    isMsi ? PackageType.Msi : PackageType.Exe, $"Uninstall registry ({hive}/{view})", uninstall, quiet, $"{hive}\\{path}\\{subName}"));
            }
        }
        catch (Exception) when (hive == RegistryHive.CurrentUser) { }
    }

    private static void AddAppxPackages(List<InventoryItem> items)
    {
        const string script = "Get-AppxPackage -AllUsers | Select-Object Name,PackageFullName,Publisher,Version | ConvertTo-Json -Compress";
        foreach (var item in RunPowerShellJson(script))
        {
            var name = Property(item, "Name"); var fullName = Property(item, "PackageFullName");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fullName)) continue;
            items.Add(new InventoryItem(name, name, Property(item, "Version", "Unknown"), Property(item, "Publisher", "Unknown"), PackageType.Appx, "AppX package metadata", PackageFullName: fullName));
        }
    }

    private static void AddOemServices(List<InventoryItem> items)
    {
        TryWmi("SELECT Name, DisplayName, PathName, State FROM Win32_Service", o =>
        {
            var name = Text(o["Name"], ""); var display = Text(o["DisplayName"], name); var path = Text(o["PathName"], "");
            if (!ContainsMarker(name + " " + display + " " + path)) return;
            items.Add(new InventoryItem(name, display, Text(o["State"], "Unknown"), "Windows Service", PackageType.Service, "Win32_Service", RegistryPath: $"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{name}"));
        });
    }

    private static void AddOemScheduledTasks(List<InventoryItem> items)
    {
        const string script = "Get-ScheduledTask | Select-Object TaskName,TaskPath,State | ConvertTo-Json -Compress";
        foreach (var item in RunPowerShellJson(script))
        {
            var name = Property(item, "TaskName"); var path = Property(item, "TaskPath");
            if (!ContainsMarker(name + " " + path)) continue;
            items.Add(new InventoryItem(path + name, name, Property(item, "State", "Unknown"), "Scheduled Task", PackageType.ScheduledTask, "ScheduledTasks metadata", RegistryPath: path));
        }
    }

    private static IReadOnlyList<string> GetSecurityProducts()
    {
        var products = new List<string>();
        try { using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT displayName FROM AntiVirusProduct"); foreach (ManagementObject o in searcher.Get()) products.Add(Text(o["displayName"], "Unknown")); }
        catch (ManagementException) { }
        return products.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsRebootPending()
    {
        using var cbs = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
        using var wu = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
        using var session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
        return cbs is not null || wu is not null || session?.GetValue("PendingFileRenameOperations") is not null;
    }

    private static IReadOnlyList<JsonElement> RunPowerShellJson(string script)
    {
        var results = new List<JsonElement>();
        var exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(exe)) return results;
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start); if (process is null) return results;
        var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000)) { try { process.Kill(true); } catch (InvalidOperationException) { } return results; }
        Task.WaitAll([outputTask, errorTask], 2000); var output = outputTask.IsCompletedSuccessfully ? outputTask.Result : "";
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return results;
        JsonDocument doc; try { doc = JsonDocument.Parse(output); } catch (JsonException) { return results; }
        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array) results.AddRange(doc.RootElement.EnumerateArray().Select(x => x.Clone()));
            else if (doc.RootElement.ValueKind == JsonValueKind.Object) results.Add(doc.RootElement.Clone());
        }
        return results;
    }

    private static void TryWmi(string query, Action<ManagementObject> action) { try { using var searcher = new ManagementObjectSearcher(query); foreach (ManagementObject o in searcher.Get()) action(o); } catch (ManagementException) { } }
    private static string? RegistryValue(string path, string name) { using var key = Registry.LocalMachine.OpenSubKey(path); return key?.GetValue(name)?.ToString(); }
    private static string Text(object? value, string fallback) => string.IsNullOrWhiteSpace(value?.ToString()) ? fallback : value!.ToString()!;
    private static string Property(JsonElement e, string name, string fallback = "") => e.TryGetProperty(name, out var p) ? p.ToString() : fallback;
    private static bool ContainsMarker(string value) => OemMarkers.Any(m => value.Contains(m, StringComparison.OrdinalIgnoreCase));
}
