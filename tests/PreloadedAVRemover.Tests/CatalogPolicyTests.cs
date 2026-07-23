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
        foreach (var id in new[] { "hp-quickdrop", "asus-glidex", "lenovo-smart-appearance", "samsung-galaxy-book-experience", "razer-axon" })
            Assert.Contains(catalog.Entries, e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        Assert.All(catalog.Entries, e => { Assert.False(string.IsNullOrWhiteSpace(e.Notes)); Assert.False(string.IsNullOrWhiteSpace(e.DetectionMethod)); });
    }

    [Fact]
    public void EmbeddedCatalog_CoversRemoteTools_AndKeepsThemNonAutomatic()
    {
        var entries = RemovalCatalog.LoadEmbedded().Entries.Where(e => e.Id.StartsWith("remote-", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var id in new[] { "remote-connectwise", "remote-screenconnect", "remote-atera", "remote-splashtop", "remote-anydesk", "remote-teamviewer", "remote-ninjaone", "remote-kaseya", "remote-datto", "remote-nable", "remote-rustdesk" })
            Assert.Contains(entries, e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        Assert.All(entries, entry =>
        {
            Assert.Equal(RiskLevel.ManualReview, entry.RiskLevel);
            Assert.False(entry.AutomaticRemovalSupported);
        });
    }

    [Theory]
    [InlineData("Atera Agent", "Atera", "remote-atera")]
    [InlineData("Splashtop Streamer", "Splashtop Inc.", "remote-splashtop")]
    [InlineData("AnyDesk", "AnyDesk Software GmbH", "remote-anydesk")]
    [InlineData("RustDesk", "RustDesk", "remote-rustdesk")]
    public void RemoteTools_AreDetectedAndRequireInvestigation(string name, string publisher, string expectedId)
    {
        var item = TestData.Msi(name) with { Publisher = publisher };
        var match = Assert.Single(RemovalCatalog.LoadEmbedded().Match([item], TestData.Device(), new CleanupPolicy()));
        Assert.Equal(expectedId, match.Catalog.Id);
        Assert.True(SoftwareClassification.IsRemoteManagementTool(match));
        Assert.Equal("Investigate", SoftwareClassification.RemoteDisposition(match));
        Assert.NotEqual(DecisionAction.Remove, match.Decision.Action);
    }

    [Theory]
    [InlineData("ConnectWise Automate", "ConnectWise")]
    [InlineData("ScreenConnect Client", "ScreenConnect Software")]
    public void ConnectWiseProducts_AreApprovedEvenUnderAggressiveWildcardBlockList(string name, string publisher)
    {
        var item = TestData.Msi(name) with { Publisher = publisher };
        var policy = new CleanupPolicy { Profile = PolicyProfile.Aggressive, BlockList = ["*"], AllowRemoteManagementRemoval = true };
        var match = Assert.Single(RemovalCatalog.LoadEmbedded().Match([item], TestData.Device(), policy));
        Assert.Equal("Approved", SoftwareClassification.RemoteDisposition(match));
        Assert.Equal(DecisionAction.Skip, match.Decision.Action);
        Assert.Contains("Approved ConnectWise", match.Decision.Reason);
    }

    [Fact]
    public void NonApprovedRemoteTool_RequiresExplicitAuthorizationBeforeRemoval()
    {
        var item = TestData.Msi("AnyDesk") with { Publisher = "AnyDesk Software GmbH" };
        var catalog = RemovalCatalog.LoadEmbedded();
        var defaultDecision = Assert.Single(catalog.Match([item], TestData.Device(), new CleanupPolicy { Profile = PolicyProfile.Balanced })).Decision;
        var authorizedDecision = Assert.Single(catalog.Match([item], TestData.Device(), new CleanupPolicy { Profile = PolicyProfile.Balanced, AllowRemoteManagementRemoval = true })).Decision;
        Assert.Equal(DecisionAction.ManualReview, defaultDecision.Action);
        Assert.Equal(DecisionAction.Remove, authorizedDecision.Action);
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

    [Fact]
    public void CatalogConstructor_RejectsDuplicateIdsAndMalformedRegex()
    {
        var duplicate = TestData.Entry();
        Assert.Throws<InvalidDataException>(() => new RemovalCatalog([duplicate, TestData.Entry(PackageType.Exe)]));
        Assert.Throws<InvalidDataException>(() => new RemovalCatalog([new CatalogEntry
        {
            Id = "bad", Vendor = "Test", Brand = "Any", ProductPattern = "[", PackageType = PackageType.Msi,
            RiskLevel = RiskLevel.Safe, DetectionMethod = "Mock", Notes = "Test"
        }]));
    }

    [Fact]
    public void AmbiguousTopMatches_FailClosedWithRationale()
    {
        CatalogEntry Entry(string id) => new()
        {
            Id = id,
            Vendor = "Dell",
            Brand = "Dell",
            ProductPattern = "OEM Utility",
            PackageType = PackageType.Msi,
            RiskLevel = RiskLevel.Safe,
            DetectionMethod = "Mock",
            Notes = "Test"
        };
        var matches = new RemovalCatalog([Entry("one"), Entry("two")]).Match([TestData.Msi("OEM Utility")], TestData.Device(), new CleanupPolicy());
        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => { Assert.Equal(DecisionAction.ManualReview, match.Decision.Action); Assert.True(match.MatchConfidence >= 70); Assert.NotEmpty(match.MatchRationale!); });
    }

    [Fact]
    public void ActiveSecurityCenterProduct_RequiresExplicitAuthorizationEvenIfCatalogFlagIsWrong()
    {
        var catalog = new RemovalCatalog([TestData.Entry(risk: RiskLevel.Safe, pattern: "Security Suite")]);
        var item = TestData.Msi("Security Suite");
        var device = TestData.Device(securityProducts: ["Security Suite"]);
        Assert.Equal(DecisionAction.AuditOnly, Assert.Single(catalog.Match([item], device, new CleanupPolicy())).Decision.Action);
        Assert.Equal(DecisionAction.Remove, Assert.Single(catalog.Match([item], device, new CleanupPolicy { AllowSecurityProductRemoval = true })).Decision.Action);
    }

    [Theory]
    [InlineData("Microsoft Defender Antivirus")]
    [InlineData("OEM Recovery Manager")]
    [InlineData("Warranty Service")]
    public void SecurityRecoveryAndWarrantySoftware_RemainProtected(string name)
    {
        var policy = new CleanupPolicy { Profile = PolicyProfile.Aggressive, BlockList = ["*"] };
        Assert.Equal(DecisionAction.Skip, PolicyEvaluator.Evaluate(TestData.Msi(name), TestData.Entry(), policy).Action);
    }
}
