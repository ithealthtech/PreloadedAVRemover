using System.Text.RegularExpressions;

namespace PreloadedAVRemover.Core;

public static partial class FriendlyDisplay
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Clipchamp.Clipchamp"] = "Clipchamp",
        ["Microsoft.MicrosoftSolitaireCollection"] = "Microsoft Solitaire",
        ["Microsoft.WindowsFeedbackHub"] = "Windows Feedback Hub",
        ["Microsoft.GetHelp"] = "Get Help",
        ["Microsoft.Getstarted"] = "Windows Tips",
        ["Microsoft.GamingApp"] = "Xbox",
        ["Microsoft.BingNews"] = "Microsoft News",
        ["Microsoft.BingWeather"] = "Microsoft Weather"
    };

    public static string ProductName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "Unknown application";
        var name = rawName.Trim();
        if (Aliases.TryGetValue(name, out var alias)) return alias;

        name = LocaleSuffix().Replace(name, string.Empty);
        name = name.Replace('_', ' ').Replace('.', ' ');
        name = AcronymBoundary().Replace(name, "$1 $2");
        name = CamelBoundary().Replace(name, " ");
        name = Whitespace().Replace(name, " ").Trim();
        name = Regex.Replace(name, @"^Bg\b", "Background", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s+(?:Background\s+)?Service$", string.Empty, RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s+Task$", string.Empty, RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(name) ? rawName.Trim() : name;
    }

    public static string PackageTypeLabel(PackageType type) => type switch
    {
        PackageType.Msi => "Desktop app",
        PackageType.Exe => "Desktop app",
        PackageType.Appx => "Microsoft Store app",
        PackageType.Winget => "Desktop app",
        PackageType.Service => "Background component",
        PackageType.ScheduledTask => "Scheduled component",
        PackageType.RegistryEntry => "System entry",
        _ => "Application"
    };

    public static string RiskLabel(RiskLevel risk) => risk switch
    {
        RiskLevel.Safe => "Low risk",
        RiskLevel.Caution => "Review first",
        RiskLevel.ManualReview => "Protected",
        _ => "Review first"
    };

    public static string DecisionLabel(DecisionAction action) => action switch
    {
        DecisionAction.Remove => "Can uninstall",
        DecisionAction.Skip => "Keep",
        DecisionAction.AuditOnly => "Audit only",
        DecisionAction.ManualReview => "Review required",
        _ => "Review required"
    };

    public static string CategoryLabel(PlanItem plan)
    {
        var id = plan.Catalog.Id;
        if (plan.Catalog.IsSecurityProduct) return "Antivirus / Security";
        if (ContainsAny(id, "command-center", "control-center", "armoury-crate", "msi-center", "optimizer", "settings", "synapse", "vantage")) return "OEM Control Panel";
        if (ContainsAny(id, "recovery", "function-key", "surface-management")) return "Hardware / Recovery";
        if (id.StartsWith("trial-", StringComparison.OrdinalIgnoreCase)) return "Trialware";
        if (id.StartsWith("appx-", StringComparison.OrdinalIgnoreCase)) return "Consumer App";
        if (plan.Inventory.PackageType is PackageType.Service or PackageType.ScheduledTask) return "Background Component";
        if (ContainsAny(id, "registration", "welcome", "jumpstart", "jumpstarts", "giftbox", "promotion", "collection", "digital-delivery", "customer-connect", "app-explorer", "bonus-apps", "axon", "cortex")) return "Bloatware";
        if (ContainsAny(id, "support", "update", "deskupdate", "service-station", "care-center", "driver-utility", "smart-assistant", "myasus")) return "OEM Support / Updates";
        return "OEM Utility";
    }

    public static string CategoryDescription(string category) => category switch
    {
        "Antivirus / Security" => "Bundled antivirus, endpoint security, or browser-security software.",
        "OEM Control Panel" => "Manufacturer software that can expose device, performance, lighting, or battery controls.",
        "Hardware / Recovery" => "Software tied to hotkeys, recovery, firmware, or hardware management; protected by default.",
        "Trialware" => "Time-limited or promotional third-party software bundled with the device.",
        "Consumer App" => "Optional consumer application delivered through Windows or the Microsoft Store.",
        "Bloatware" => "Promotional, registration, onboarding, game, or marketing software not required for core operation.",
        "OEM Support / Updates" => "Manufacturer support, diagnostics, warranty, driver, or update tooling.",
        "Background Component" => "A service or scheduled component detected for audit and manual review.",
        _ => "Optional manufacturer utility that does not fit another category."
    };

    private static bool ContainsAny(string value, params string[] tokens) => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"\s+-\s+[a-z]{2}-[a-z]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex LocaleSuffix();

    [GeneratedRegex(@"([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundary();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
