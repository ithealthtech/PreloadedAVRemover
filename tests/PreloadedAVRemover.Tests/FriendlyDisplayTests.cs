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
    public void ProductName_RemovesTechnicalJargon(string raw, string expected) => Assert.Equal(expected, FriendlyDisplay.ProductName(raw));

    [Fact]
    public void Labels_AreOperatorFriendly()
    {
        Assert.Equal("Microsoft Store app", FriendlyDisplay.PackageTypeLabel(PackageType.Appx));
        Assert.Equal("Protected", FriendlyDisplay.RiskLabel(RiskLevel.ManualReview));
        Assert.Equal("Review required", FriendlyDisplay.DecisionLabel(DecisionAction.ManualReview));
    }
}
