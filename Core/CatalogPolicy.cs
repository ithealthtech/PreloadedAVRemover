using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PreloadedAVRemover.Core;

public sealed class RemovalCatalog
{
    private readonly IReadOnlyList<CatalogEntry> _entries;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    public RemovalCatalog(IReadOnlyList<CatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var duplicate = entries.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"Duplicate catalog ID: {duplicate.Key}");
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Brand) || string.IsNullOrWhiteSpace(entry.ProductPattern) || string.IsNullOrWhiteSpace(entry.DetectionMethod) || string.IsNullOrWhiteSpace(entry.Notes))
                throw new InvalidDataException("Catalog entries require ID, brand, product pattern, detection method, and notes.");
            if (!Enum.IsDefined(entry.PackageType) || !Enum.IsDefined(entry.RiskLevel)) throw new InvalidDataException($"Catalog entry {entry.Id} contains an invalid enum value.");
            try { _ = new Regex(entry.ProductPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout); }
            catch (ArgumentException ex) { throw new InvalidDataException($"Catalog entry {entry.Id} contains an invalid product pattern.", ex); }
        }
        _entries = entries.ToArray();
    }
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
            var candidates = _entries.Where(e => e.PackageType == item.PackageType && IsNameMatch(item.Name, e.ProductPattern)).ToList();
            var exactPackageType = candidates.Count > 0;
            if (candidates.Count == 0 && item.PackageType is PackageType.Msi or PackageType.Exe)
                candidates = _entries.Where(e => e.PackageType is PackageType.Msi or PackageType.Exe && IsNameMatch(item.Name, e.ProductPattern)).ToList();

            var scored = candidates.Where(e => BrandMatches(e.Brand, device.Manufacturer, item.Publisher))
                .Select(e => Score(e, item, device, exactPackageType)).ToList();
            if (scored.Count == 0) continue;
            var highest = scored.Max(x => x.Confidence);
            var finalists = scored.Where(x => x.Confidence == highest).ToList();
            foreach (var candidate in finalists)
            {
                var decision = finalists.Count > 1
                    ? new PolicyDecision(DecisionAction.ManualReview, $"Ambiguous catalog match ({string.Join(", ", finalists.Select(x => x.Entry.Id))})")
                    : candidate.Confidence < 70
                        ? new PolicyDecision(DecisionAction.ManualReview, $"Low-confidence catalog match ({candidate.Confidence}%)")
                        : IsActiveSecurityProduct(item, device) && !policy.AllowSecurityProductRemoval
                            ? new PolicyDecision(DecisionAction.AuditOnly, "Active endpoint protection detected by Windows Security Center; explicit security-product authorization is required")
                            : PolicyEvaluator.Evaluate(item, candidate.Entry, policy);
                matches.Add(new PlanItem(item, candidate.Entry, decision, candidate.Confidence, candidate.Rationale));
            }
        }
        return matches.GroupBy(x => $"{x.Inventory.Id}|{x.Catalog.Id}", StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    private static bool IsNameMatch(string name, string pattern) => Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);

    private static (CatalogEntry Entry, int Confidence, IReadOnlyList<string> Rationale) Score(CatalogEntry entry, InventoryItem item, DeviceIdentity device, bool exactPackageType)
    {
        var rationale = new List<string> { "Product name matched catalog pattern" };
        var score = 45;
        if (exactPackageType) { score += 20; rationale.Add("Package type matched exactly"); }
        else { score += 10; rationale.Add("MSI/EXE registry installer fallback matched"); }

        if (entry.Brand.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            score += 5; rationale.Add("Catalog entry is vendor-neutral");
            if (item.Publisher.Contains(entry.Vendor, StringComparison.OrdinalIgnoreCase)) { score += 15; rationale.Add("Publisher matched catalog vendor"); }
        }
        else
        {
            if (ManufacturerMatches(entry.Brand, device.Manufacturer)) { score += 15; rationale.Add("Device manufacturer matched catalog brand"); }
            if (item.Publisher.Contains(entry.Brand, StringComparison.OrdinalIgnoreCase) || item.Publisher.Contains(entry.Vendor, StringComparison.OrdinalIgnoreCase)) { score += 15; rationale.Add("Publisher matched catalog vendor/brand"); }
        }
        if (!string.IsNullOrWhiteSpace(item.DetectionMethod)) { score += 5; rationale.Add($"Detected through {item.DetectionMethod}"); }
        return (entry, Math.Min(score, 100), rationale);
    }

    private static bool IsActiveSecurityProduct(InventoryItem item, DeviceIdentity device) =>
        device.SecurityProducts.Any(product => item.Name.Contains(product, StringComparison.OrdinalIgnoreCase) || product.Contains(item.Name, StringComparison.OrdinalIgnoreCase));

    private static bool BrandMatches(string brand, string manufacturer, string publisher) =>
        brand.Equals("Any", StringComparison.OrdinalIgnoreCase) || ManufacturerMatches(brand, manufacturer) ||
        publisher.Contains(brand, StringComparison.OrdinalIgnoreCase) ||
        publisher.Contains(brand.Equals("Alienware", StringComparison.OrdinalIgnoreCase) ? "Dell" : brand, StringComparison.OrdinalIgnoreCase);

    private static bool ManufacturerMatches(string brand, string manufacturer) => manufacturer.Contains(brand, StringComparison.OrdinalIgnoreCase) ||
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
        "microsoft defender", "windows defender", "recovery", "restore", "warranty", "sure recover",
        "chipset", "bluetooth", "wireless", "wi-fi", "ethernet", "audio driver", "graphics driver", "display driver", "hotkey", "firmware", "bios", "thunderbolt"
    ];

    public static PolicyDecision Evaluate(InventoryItem item, CatalogEntry entry, CleanupPolicy policy)
    {
        if (Matches(policy.AllowList, item.Name, entry.Id)) return new(DecisionAction.Skip, "Organization allowlist");
        if (SoftwareClassification.IsApprovedManagementTool(item, entry)) return new(DecisionAction.Skip, "Approved ConnectWise management platform");
        var catalogedRemoteTool = entry.Id.StartsWith("remote-", StringComparison.OrdinalIgnoreCase);
        if (catalogedRemoteTool && !policy.AllowRemoteManagementRemoval) return new(DecisionAction.ManualReview, "Remote-tool removal was not explicitly enabled");
        if (catalogedRemoteTool)
        {
            if (Matches(policy.BlockList, item.Name, entry.Id)) return new(DecisionAction.Remove, "Organization blocklist with explicit remote-tool authorization", true);
            return policy.Profile == PolicyProfile.Conservative
                ? new(DecisionAction.Skip, "Conservative policy requires a blocklist match for remote-tool removal")
                : new(DecisionAction.Remove, "Explicit remote-tool authorization with confirmation");
        }
        if (!catalogedRemoteTool && ProtectedPatterns.Any(p => item.Name.Contains(p, StringComparison.OrdinalIgnoreCase))) return new(DecisionAction.Skip, "Protected business-critical, driver, firmware, remote-management, backup, encryption, or VPN software");
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
