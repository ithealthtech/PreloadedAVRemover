using PreloadedAVRemover.Core;

namespace PreloadedAVRemover.Tests;

public sealed class FriendlyDisplayTests
{
    [Theory]
    [InlineData("Clipchamp.Clipchamp", "Clipchamp")]
    [InlineData("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire")]
    [InlineData("BgTaskRegistrationMaintenanceTask", "Background Task Registration Maintenance")]
    [InlineData("ASUSOptimizationService", "ASUS Optimization")]
    [InlineData("Microsoft 365 Apps for enterprise - en-us", "Microsoft 365 Apps for enterprise")]
    [InlineData("AnyDesk", "AnyDesk")]
    [InlineData("McAfee LiveSafe", "McAfee LiveSafe")]
    [InlineData("ScreenConnect Client", "ScreenConnect Client")]
    [InlineData("WildTangent Games", "WildTangent Games")]
    public void ProductName_RemovesTechnicalJargon(string raw, string expected) => Assert.Equal(expected, FriendlyDisplay.ProductName(raw));

    [Fact]
    public void Labels_AreOperatorFriendly()
    {
        Assert.Equal("Microsoft Store app", FriendlyDisplay.PackageTypeLabel(PackageType.Appx));
        Assert.Equal("Protected", FriendlyDisplay.RiskLabel(RiskLevel.ManualReview));
        Assert.Equal("Review required", FriendlyDisplay.DecisionLabel(DecisionAction.ManualReview));
    }

    [Theory]
    [InlineData("security-mcafee", true, PackageType.Exe, "Antivirus / Security")]
    [InlineData("asus-armoury-crate", false, PackageType.Exe, "OEM Control Panel")]
    [InlineData("hp-registration", false, PackageType.Exe, "Bloatware")]
    [InlineData("trial-wildtangent", false, PackageType.Exe, "Trialware")]
    [InlineData("appx-clipchamp", false, PackageType.Appx, "Consumer App")]
    [InlineData("dell-supportassist", false, PackageType.Exe, "OEM Support / Updates")]
    [InlineData("uncataloged-task", false, PackageType.ScheduledTask, "Background Component")]
    [InlineData("hp-registration-task", false, PackageType.ScheduledTask, "Background Component")]
    public void CategoryLabel_DistinguishesSoftwareTypes(string id, bool security, PackageType type, string expected)
    {
        var inventory = new InventoryItem("inventory", "Example", "1", "Vendor", type, "Test");
        var catalog = TestData.Entry(type, security: security, id: id);
        Assert.Equal(expected, FriendlyDisplay.CategoryLabel(TestData.Plan(inventory, catalog)));
    }

    [Theory]
    [InlineData("remote-connectwise", "ConnectWise Automate", "Approved Management")]
    [InlineData("remote-anydesk", "AnyDesk", "Remote / Management")]
    public void CategoryLabel_DistinguishesApprovedAndInvestigatedRemoteTools(string id, string name, string expected)
    {
        var inventory = new InventoryItem("inventory", name, "1", name, PackageType.Exe, "Test");
        Assert.Equal(expected, FriendlyDisplay.CategoryLabel(TestData.Plan(inventory, TestData.Entry(PackageType.Exe, id: id))));
    }
}
