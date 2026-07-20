using System.Reflection;

namespace PreloadedAVRemover.Tests;

public sealed class ProductInfoTests
{
    [Fact]
    public void Version_MatchesAssemblyInformationalVersionWithoutBuildMetadata()
    {
        var informationalVersion = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        Assert.Equal(informationalVersion.Split('+')[0], ProductInfo.Version);
        Assert.DoesNotContain('+', ProductInfo.Version);
    }
}
