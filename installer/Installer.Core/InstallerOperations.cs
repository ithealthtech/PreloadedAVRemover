using System.IO.Compression;
using System.Security.Cryptography;

namespace PreloadedAVRemover.Installer;

public enum InstallMode
{
    Everyone,
    CurrentUser,
    Portable
}

public static class InstallerOperations
{
    public const string PortableFolderName = "OEM Endpoint Cleanup Portable";

    public static string ResolvePayloadPath(string baseDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("A base directory is required.", nameof(baseDirectory));
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Payload names must be plain file names.", nameof(fileName));

        var root = Path.GetFullPath(baseDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, fileName));
        if (!candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payload path escaped the installer directory.");
        return candidate;
    }

    public static bool HasExpectedSha256(string path, string expectedSha256)
    {
        if (!File.Exists(path) || expectedSha256.Length != 64 || expectedSha256.Any(c => !Uri.IsHexDigit(c))) return false;
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> BuildMsiArguments(InstallMode mode, string msiPath)
    {
        if (mode == InstallMode.Portable) throw new ArgumentException("Portable mode does not invoke Windows Installer.", nameof(mode));
        if (!Path.IsPathFullyQualified(msiPath)) throw new ArgumentException("The MSI path must be absolute.", nameof(msiPath));

        var result = new List<string> { "/i", msiPath, "/passive", "/norestart" };
        if (mode == InstallMode.Everyone)
        {
            result.Add("ALLUSERS=1");
            result.Add("MSIINSTALLPERUSER=");
        }
        else
        {
            result.Add("ALLUSERS=2");
            result.Add("MSIINSTALLPERUSER=1");
        }
        return result;
    }

    public static string GetPortableDestination(string selectedDirectory) =>
        Path.Combine(Path.GetFullPath(selectedDirectory), PortableFolderName);

    public static void ExtractZipSafely(string zipPath, string destinationDirectory)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(destinationRoot);
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Archive entry escapes the destination: {entry.FullName}");

            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType == 0xA000) throw new InvalidDataException($"Symbolic links are not permitted: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }
}
