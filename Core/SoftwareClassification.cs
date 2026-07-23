namespace PreloadedAVRemover.Core;

public static class SoftwareClassification
{
    private static readonly string[] ApprovedManagementPatterns = ["connectwise", "screenconnect"];

    public static bool IsRemoteManagementTool(PlanItem plan) =>
        plan.Catalog.Id.StartsWith("remote-", StringComparison.OrdinalIgnoreCase);

    public static bool IsApprovedManagementTool(InventoryItem item, CatalogEntry entry) =>
        ApprovedManagementPatterns.Any(pattern =>
            item.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            item.Publisher.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            entry.Id.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            entry.Vendor.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    public static string RemoteDisposition(PlanItem plan) =>
        IsApprovedManagementTool(plan.Inventory, plan.Catalog) ? "Approved" : "Investigate";
}
