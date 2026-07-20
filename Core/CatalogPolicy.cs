using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PreloadedAVRemover.Core;

public sealed class RemovalCatalog
{
    private readonly IReadOnlyList<CatalogEntry> _entries;
    public RemovalCatalog(IReadOnlyList<CatalogEntry> entries) => _entries = entries;
    public IReadOnlyList<CatalogEntry> Entries => _entries;

    public static RemovalCatalog LoadEmbedded()
    {
        var assembly = typeof(RemovalCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("oem-removal-catalog.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("Embedded removal catalog not found.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        return new RemovalCatalog(JsonSerializer.Deserialize<List<CatalogEntry>>(stream, options) ?? []);
    }

    public IReadOnlyList<PlanItem> Match(IEnumerable<InventoryItem> inventory, DeviceIdentity device, CleanupPolicy policy)
    {
        var matches = new List<PlanItem>();
        foreach (var item in inventory)
        {
            var candidates = _entries.Where(e => e.PackageType == item.PackageType && Regex.IsMatch(item.Name, e.ProductPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
            if (candidates.Count == 0 && item.PackageType is PackageType.Msi or PackageType.Exe)
                candidates = _entries.Where(e => e.PackageType is PackageType.Msi or PackageType.Exe && Regex.IsMatch(item.Name, e.ProductPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
            foreach (var entry in candidates)
            {
                if (!BrandMatches(entry.Brand, device.Manufacturer, item.Publisher)) continue;
                matches.Add(new PlanItem(item, entry, PolicyEvaluator.Evaluate(item, entry, policy)));
            }
        }
        return matches.GroupBy(x => $"{x.Inventory.Id}|{x.Catalog.Id}", StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    private static bool BrandMatches(string brand, string manufacturer, string publisher) =>
        brand.Equals("Any", StringComparison.OrdinalIgnoreCase) || manufacturer.Contains(brand, StringComparison.OrdinalIgnoreCase) ||
        publisher.Contains(brand, StringComparison.OrdinalIgnoreCase) ||
        (brand.Equals("Alienware", StringComparison.OrdinalIgnoreCase) && manufacturer.Contains("Dell", StringComparison.OrdinalIgnoreCase)) ||
        (brand.Equals("Dynabook", StringComparison.OrdinalIgnoreCase) && (manufacturer.Contains("TOSHIBA", StringComparison.OrdinalIgnoreCase) || manufacturer.Contains("Dynabook", StringComparison.OrdinalIgnoreCase)));
}

public static class PolicyEvaluator
{
    private static readonly string[] ProtectedPatterns =
    [
        "connectwise", "screenconnect", "kaseya", "datto", "n-able", "ncentral", "n-sight", "atera", "ninjaone", "syncro", "action1",
        "teamviewer", "splashtop", "anydesk", "beyondtrust", "bomgar", "logmein", "goto resolve", "rmm", "remote monitoring",
        "bitlocker", "veeam", "acronis", "carbonite", "backblaze", "crashplan", "backup agent",
        "forticlient", "globalprotect", "cisco secure client", "anyconnect", "zscaler", "netskope", "openvpn", "wireguard",
        "chipset", "bluetooth", "wireless", "wi-fi", "ethernet", "audio driver", "graphics driver", "display driver", "hotkey", "firmware", "bios", "thunderbolt"
    ];

    public static PolicyDecision Evaluate(InventoryItem item, CatalogEntry entry, CleanupPolicy policy)
    {
        if (Matches(policy.AllowList, item.Name, entry.Id)) return new(DecisionAction.Skip, "Organization allowlist");
        if (ProtectedPatterns.Any(p => item.Name.Contains(p, StringComparison.OrdinalIgnoreCase))) return new(DecisionAction.Skip, "Protected business-critical, driver, firmware, remote-management, backup, encryption, or VPN software");
        if (entry.IsSecurityProduct && !policy.AllowSecurityProductRemoval) return new(DecisionAction.AuditOnly, "Endpoint protection removal was not explicitly enabled");
        if (!entry.AutomaticRemovalSupported) return new(DecisionAction.ManualReview, "Catalog entry does not permit generalized automatic removal");
        if (Matches(policy.BlockList, item.Name, entry.Id)) return new(DecisionAction.Remove, "Organization blocklist", true);
        if (entry.RiskLevel == RiskLevel.ManualReview) return new(DecisionAction.ManualReview, "Catalog risk is manual-review");
        return policy.Profile switch
        {
            PolicyProfile.Conservative when entry.RiskLevel != RiskLevel.Safe => new(DecisionAction.Skip, "Conservative policy removes only safe entries"),
            PolicyProfile.Balanced when entry.RiskLevel == RiskLevel.Caution => new(DecisionAction.Remove, "Balanced policy permits cataloged caution entries with confirmation"),
            PolicyProfile.Aggressive => new(DecisionAction.Remove, "Aggressive policy permits cataloged safe and caution entries"),
            _ => new(DecisionAction.Remove, "Cataloged safe removal")
        };
    }

    private static bool Matches(IEnumerable<string> patterns, string name, string id) => patterns.Any(p => !string.IsNullOrWhiteSpace(p) && (Wildcard(name, p) || Wildcard(id, p)));
    private static bool Wildcard(string value, string pattern) => Regex.IsMatch(value, "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
}
