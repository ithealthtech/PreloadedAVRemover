using PreloadedAVRemover.Core;

namespace PreloadedAVRemover.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void MissingPolicyFile_ReturnsConservativeDryRunDefaults()
    {
        var policy = PolicyConfiguration.Load(Path.Combine(TestData.TempDirectory(), "missing.json"));
        Assert.Equal(PolicyProfile.Conservative, policy.Profile); Assert.True(policy.DryRun); Assert.False(policy.AllowSecurityProductRemoval); Assert.False(policy.AllowRemoteManagementRemoval);
    }

    [Fact]
    public void PolicyFile_LoadsProfileAndLists()
    {
        var path = Path.Combine(TestData.TempDirectory(), "policy.json");
        File.WriteAllText(path, """{"profile":"Aggressive","dryRun":false,"force":true,"allowRemoteManagementRemoval":true,"allowList":["Keep*"],"blockList":["Remove*"]}""");
        var policy = PolicyConfiguration.Load(path);
        Assert.Equal(PolicyProfile.Aggressive, policy.Profile); Assert.False(policy.DryRun); Assert.True(policy.Force); Assert.True(policy.AllowRemoteManagementRemoval);
        Assert.Equal("Keep*", policy.AllowList.Single()); Assert.Equal("Remove*", policy.BlockList.Single());
    }

    [Fact]
    public void MalformedPolicy_FailsClosedToConservativeDryRun()
    {
        var path = Path.Combine(TestData.TempDirectory(), "policy.json"); File.WriteAllText(path, "{not-json");
        var policy = PolicyConfiguration.Load(path);
        Assert.True(policy.DryRun); Assert.Equal(PolicyProfile.Conservative, policy.Profile); Assert.False(policy.AllowSecurityProductRemoval); Assert.False(policy.AllowRemoteManagementRemoval);
    }

    [Fact]
    public void EmbeddedCatalog_PreservesCriticalHardwareControlEntries()
    {
        var catalog = RemovalCatalog.LoadEmbedded();
        foreach (var id in new[] { "alienware-command-center", "asus-armoury-crate", "msi-center", "samsung-settings", "toshiba-function-key", "lg-control-center", "gigabyte-control-center", "razer-synapse" })
        {
            var entry = Assert.Single(catalog.Entries, x => x.Id == id);
            Assert.Equal(RiskLevel.ManualReview, entry.RiskLevel); Assert.False(entry.AutomaticRemovalSupported); Assert.NotEmpty(entry.KnownDependencies);
        }
    }

    [Fact]
    public void EmbeddedAvCatalog_RegressionRequiresExplicitAuthorization()
    {
        var catalog = RemovalCatalog.LoadEmbedded();
        var item = new InventoryItem("id", "McAfee LiveSafe", "1", "McAfee", PackageType.Exe, "Registry", @"C:\Program Files\McAfee\uninstall.exe");
        var defaultMatch = Assert.Single(catalog.Match([item], TestData.Device("Dell"), new CleanupPolicy()), x => x.Catalog.Id == "security-mcafee");
        Assert.Equal(DecisionAction.AuditOnly, defaultMatch.Decision.Action);
        var explicitMatch = Assert.Single(catalog.Match([item], TestData.Device("Dell"), new CleanupPolicy { Profile = PolicyProfile.Balanced, AllowSecurityProductRemoval = true }), x => x.Catalog.Id == "security-mcafee");
        Assert.Equal(DecisionAction.Remove, explicitMatch.Decision.Action);
    }
}
