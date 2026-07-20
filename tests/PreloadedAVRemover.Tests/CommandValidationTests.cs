using PreloadedAVRemover.Core;

namespace PreloadedAVRemover.Tests;

public sealed class CommandValidationTests
{
    [Fact]
    public void MsiCommand_IsCanonicalAndDoesNotUseRegistryArguments()
    {
        var result = CommandValidator.Validate(TestData.Plan(TestData.Msi(), TestData.Entry()));
        Assert.True(result.IsValid); Assert.EndsWith("msiexec.exe", result.Command!.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["/x", "{11111111-1111-1111-1111-111111111111}", "/qn", "/norestart"], result.Command.Arguments);
    }

    [Theory]
    [InlineData("cmd.exe /c calc.exe")]
    [InlineData("powershell.exe -Command Remove-Item C:\\")]
    [InlineData("relative-uninstall.exe /s")]
    [InlineData("C:\\Missing\\uninstall.exe /s")]
    [InlineData("\r\n")]
    public void MalformedOrUnsafeExeCommands_AreRejected(string command)
    {
        var item = new InventoryItem("id", "OEM App", "1", "OEM", PackageType.Exe, "Registry", command);
        Assert.False(CommandValidator.Validate(TestData.Plan(item, TestData.Entry(PackageType.Exe))).IsValid);
    }

    [Fact]
    public void ExistingDirectExe_IsParsedWithoutShell()
    {
        var executable = Environment.ProcessPath!;
        var item = new InventoryItem("id", "OEM App", "1", "OEM", PackageType.Exe, "Registry", $"\"{executable}\" --example");
        var result = CommandValidator.Validate(TestData.Plan(item, TestData.Entry(PackageType.Exe)));
        Assert.True(result.IsValid); Assert.Equal(executable, result.Command!.FileName); Assert.Equal("--example", result.Command.Arguments.Single());
    }

    [Fact]
    public void UnquotedExistingExePathWithSpaces_IsNormalizedSafely()
    {
        var directory = Path.Combine(TestData.TempDirectory(), "Folder With Spaces"); Directory.CreateDirectory(directory);
        var copy = Path.Combine(directory, "uninstall.exe"); File.Copy(Environment.ProcessPath!, copy);
        var item = new InventoryItem("id", "OEM App", "1", "OEM", PackageType.Exe, "Registry", $"{copy} /silent");
        var result = CommandValidator.Validate(TestData.Plan(item, TestData.Entry(PackageType.Exe)));
        Assert.True(result.IsValid); Assert.Equal(copy, result.Command!.FileName); Assert.Equal("/silent", result.Command.Arguments.Single());
    }

    [Fact]
    public void AppxPackageName_IsValidated()
    {
        var good = new InventoryItem("app", "Clipchamp", "1", "Microsoft", PackageType.Appx, "AppX", PackageFullName: "Clipchamp.Clipchamp_1.2.3_x64__abc");
        var bad = good with { PackageFullName = "bad'; Remove-Item C:\\; '" };
        Assert.True(CommandValidator.Validate(TestData.Plan(good, TestData.Entry(PackageType.Appx))).IsValid);
        Assert.False(CommandValidator.Validate(TestData.Plan(bad, TestData.Entry(PackageType.Appx))).IsValid);
    }

    [Fact]
    public void MissingWinget_FailsSafely()
    {
        var original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "");
            var item = new InventoryItem("Vendor.Package", "App", "1", "Vendor", PackageType.Winget, "Mock");
            var result = CommandValidator.Validate(TestData.Plan(item, TestData.Entry(PackageType.Winget)));
            Assert.False(result.IsValid); Assert.Contains("unavailable", result.Reason);
        }
        finally { Environment.SetEnvironmentVariable("PATH", original); }
    }

    [Theory]
    [InlineData(PackageType.Service)]
    [InlineData(PackageType.ScheduledTask)]
    [InlineData(PackageType.RegistryEntry)]
    public void UnsupportedDestructiveBackends_FailClosed(PackageType type)
    {
        var item = new InventoryItem("id", "Item", "1", "OEM", type, "Mock");
        Assert.False(CommandValidator.Validate(TestData.Plan(item, TestData.Entry(type))).IsValid);
    }
}
