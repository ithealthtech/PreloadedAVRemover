using PreloadedAVRemover.Core;

namespace PreloadedAVRemover.Tests;

public sealed class CatalogPolicyTests
{
    [Fact]
    public void EmbeddedCatalog_CoversRequiredBrandsAndPackageTypes()
    {
        var catalog = RemovalCatalog.LoadEmbedded();
        foreach (var brand in new[] { "Dell", "HP", "ASUS", "Acer", "Lenovo", "MSI", "Samsung", "Dynabook", "Microsoft", "LG", "Gigabyte", "Razer", "Alienware", "Fujitsu" })
            Assert.Contains(catalog.Entries, e => e.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase));
        foreach (var type in Enum.GetValues<PackageType>()) Assert.Contains(catalog.Entries, e => e.PackageType == type);
        Assert.All(catalog.Entries, e => { Assert.False(string.IsNullOrWhiteSpace(e.Notes)); Assert.False(string.IsNullOrWhiteSpace(e.DetectionMethod)); });
    }

    [Fact]
    public void CatalogMatching_UsesDeviceBrandAndProductPattern()
    {
        var catalog = new RemovalCatalog([TestData.Entry(brand: "Dell", pattern: "Support Trial")]);
        var matches = catalog.Match([TestData.Msi("Support Trial")], TestData.Device("Dell Inc."), new CleanupPolicy());
        Assert.Single(matches);
        var nonDellItem = TestData.Msi("Support Trial") with { Publisher = "Unknown" };
        Assert.Empty(catalog.Match([nonDellItem], TestData.Device("HP"), new CleanupPolicy()));
    }

    [Fact]
    public void RegistryInstallerType_FallsBackBetweenExeAndMsiWhenNoExactCatalogEntryExists()
    {
        var catalog = new RemovalCatalog([TestData.Entry(PackageType.Exe, brand: "Dell", pattern: "OEM Utility")]);
        Assert.Single(catalog.Match([TestData.Msi("OEM Utility")], TestData.Device(), new CleanupPolicy()));
    }

    [Theory]
    [InlineData(PolicyProfile.Conservative, RiskLevel.Safe, DecisionAction.Remove)]
    [InlineData(PolicyProfile.Conservative, RiskLevel.Caution, DecisionAction.Skip)]
    [InlineData(PolicyProfile.Balanced, RiskLevel.Caution, DecisionAction.Remove)]
    [InlineData(PolicyProfile.Aggressive, RiskLevel.Caution, DecisionAction.Remove)]
    [InlineData(PolicyProfile.Aggressive, RiskLevel.ManualReview, DecisionAction.ManualReview)]
    public void PolicyProfiles_ApplyRiskRules(PolicyProfile profile, RiskLevel risk, DecisionAction expected)
    {
        var decision = PolicyEvaluator.Evaluate(TestData.Msi(), TestData.Entry(risk: risk), new CleanupPolicy { Profile = profile });
        Assert.Equal(expected, decision.Action);
    }

    [Fact]
    public void AllowList_WinsOverBlockList()
    {
        var policy = new CleanupPolicy { AllowList = ["OEM Trial"], BlockList = ["OEM*"] };
        Assert.Equal(DecisionAction.Skip, PolicyEvaluator.Evaluate(TestData.Msi(), TestData.Entry(), policy).Action);
    }

    [Fact]
    public void BlockList_ForcesCatalogedRemoval()
    {
        var decision = PolicyEvaluator.Evaluate(TestData.Msi(), TestData.Entry(risk: RiskLevel.Caution), new CleanupPolicy { Profile = PolicyProfile.Conservative, BlockList = ["OEM*"] });
        Assert.Equal(DecisionAction.Remove, decision.Action); Assert.True(decision.Forced);
    }

    [Theory]
    [InlineData("NinjaOne Agent")]
    [InlineData("ConnectWise ScreenConnect")]
    [InlineData("Veeam Backup Agent")]
    [InlineData("GlobalProtect VPN")]
    [InlineData("Dell BIOS Firmware")]
    [InlineData("Realtek Audio Driver")]
    public void ProtectedSoftware_IsNeverRemovedByPolicy(string name)
    {
        var item = TestData.Msi(name);
        var policy = new CleanupPolicy { Profile = PolicyProfile.Aggressive, BlockList = ["*"] };
        Assert.Equal(DecisionAction.Skip, PolicyEvaluator.Evaluate(item, TestData.Entry(), policy).Action);
    }

    [Fact]
    public void SecurityProducts_DefaultToAuditOnly_AndRequireExplicitAuthorization()
    {
        var entry = TestData.Entry(security: true, risk: RiskLevel.Caution);
        Assert.Equal(DecisionAction.AuditOnly, PolicyEvaluator.Evaluate(TestData.Msi("McAfee LiveSafe"), entry, new CleanupPolicy { Profile = PolicyProfile.Balanced }).Action);
        Assert.Equal(DecisionAction.Remove, PolicyEvaluator.Evaluate(TestData.Msi("McAfee LiveSafe"), entry, new CleanupPolicy { Profile = PolicyProfile.Balanced, AllowSecurityProductRemoval = true }).Action);
    }

    [Fact]
    public void MockedDetection_CoversEveryPackageType()
    {
        foreach (var type in Enum.GetValues<PackageType>())
        {
            var item = new InventoryItem("id", "Mock OEM Item", "1", "Dell", type, "Mock");
            var catalog = new RemovalCatalog([TestData.Entry(type, brand: "Dell", pattern: "Mock OEM Item")]);
            Assert.Single(catalog.Match([item], TestData.Device(), new CleanupPolicy()));
        }
    }
}
