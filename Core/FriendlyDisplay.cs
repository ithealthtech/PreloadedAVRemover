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

    [GeneratedRegex(@"\s+-\s+[a-z]{2}-[a-z]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex LocaleSuffix();

    [GeneratedRegex(@"([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundary();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
