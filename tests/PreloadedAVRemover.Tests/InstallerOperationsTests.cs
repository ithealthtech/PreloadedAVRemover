using PreloadedAVRemover.Installer;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PreloadedAVRemover.Tests;

public sealed class InstallerOperationsTests
{
    [Fact]
    public void MsiArguments_SelectMachineScopeWithoutShellText()
    {
        var path = Path.GetFullPath("cleanup.msi");
        var args = InstallerOperations.BuildMsiArguments(InstallMode.Everyone, path);
        Assert.Equal(new[] { "/i", path, "/passive", "/norestart", "ALLUSERS=1", "MSIINSTALLPERUSER=" }, args);
    }

    [Fact]
    public void MsiArguments_SelectCurrentUserScope()
    {
        var path = Path.GetFullPath("cleanup.msi");
        var args = InstallerOperations.BuildMsiArguments(InstallMode.CurrentUser, path);
        Assert.Contains("ALLUSERS=2", args);
        Assert.Contains("MSIINSTALLPERUSER=1", args);
    }

    [Fact]
    public void PayloadPath_RejectsTraversal()
    {
        Assert.Throws<ArgumentException>(() => InstallerOperations.ResolvePayloadPath(Path.GetTempPath(), "..\\bad.msi"));
    }

    [Fact]
    public void HashValidation_UsesSha256()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "trusted payload");
            var expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            Assert.True(InstallerOperations.HasExpectedSha256(file, expected));
            Assert.False(InstallerOperations.HasExpectedSha256(file, new string('0', 64)));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void PortableExtraction_RejectsZipTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zip = Path.Combine(root, "bad.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) archive.CreateEntry("../escaped.txt");
        try { Assert.Throws<InvalidDataException>(() => InstallerOperations.ExtractZipSafely(zip, Path.Combine(root, "out"))); }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void PortableExtraction_ExtractsValidatedArchive()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "PreloadedAVRemover.exe"), "test executable");
        var zip = Path.Combine(root, "portable.zip");
        ZipFile.CreateFromDirectory(source, zip);
        var destination = Path.Combine(root, "out");
        try
        {
            InstallerOperations.ExtractZipSafely(zip, destination);
            Assert.Equal("test executable", File.ReadAllText(Path.Combine(destination, "PreloadedAVRemover.exe")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
