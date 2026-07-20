using PreloadedAVRemover.Core;

namespace PreloadedAVRemover.Tests;

internal sealed class MockInventoryProvider : IInventoryProvider
{
    public DeviceIdentity Device { get; set; } = TestData.Device();
    public IReadOnlyList<InventoryItem> Items { get; set; } = [];
    public DeviceIdentity GetDeviceIdentity() => Device;
    public IReadOnlyList<InventoryItem> GetInventory() => Items;
}

internal sealed class FakeRunner : IProcessRunner
{
    public int ExitCode { get; set; }
    public int Calls { get; private set; }
    public ValidatedCommand? LastCommand { get; private set; }
    public Task<int> RunAsync(ValidatedCommand command, CancellationToken cancellationToken = default) { Calls++; LastCommand = command; return Task.FromResult(ExitCode); }
}

internal static class TestData
{
    public static DeviceIdentity Device(string manufacturer = "Dell Inc.", bool admin = true) => new("TESTPC", manufacturer, "Model X", "1.2.3", "SERIAL", "Windows", "10.0", "DOMAIN\\tech", admin, false, []);
    public static CatalogEntry Entry(PackageType type = PackageType.Msi, RiskLevel risk = RiskLevel.Safe, bool security = false, bool automatic = true, string brand = "Any", string pattern = ".*") => new()
    {
        Id = "test-entry", Vendor = "Test", Brand = brand, ProductPattern = pattern, PackageType = type, RiskLevel = risk,
        DetectionMethod = "Mock", IsSecurityProduct = security, AutomaticRemovalSupported = automatic, Notes = "Test"
    };
    public static InventoryItem Msi(string name = "OEM Trial") => new("{11111111-1111-1111-1111-111111111111}", name, "1.0", "Dell", PackageType.Msi, "Mock", "MsiExec.exe /I{11111111-1111-1111-1111-111111111111}");
    public static PlanItem Plan(InventoryItem item, CatalogEntry entry, DecisionAction action = DecisionAction.Remove) => new(item, entry, new(action, "test"));
    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OemCleanupTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
    }
}
