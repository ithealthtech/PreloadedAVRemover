using System.Text.Json;

namespace PreloadedAVRemover.Core;

public static class PolicyConfiguration
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static CleanupPolicy Load(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return new CleanupPolicy { ReportDirectory = DefaultReportDirectory() };
        try { return JsonSerializer.Deserialize<CleanupPolicy>(File.ReadAllText(path), Options) ?? new CleanupPolicy { ReportDirectory = DefaultReportDirectory() }; }
        catch (JsonException) { return new CleanupPolicy { DryRun = true, Profile = PolicyProfile.Conservative, ReportDirectory = DefaultReportDirectory() }; }
    }

    public static string DefaultPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OemCleanup", "policy.json");
    public static string DefaultReportDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OemCleanup", "Reports");
}
