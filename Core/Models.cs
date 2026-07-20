using System.Text.Json.Serialization;

namespace PreloadedAVRemover.Core;

public enum PackageType { Msi, Exe, Appx, Winget, Service, ScheduledTask, RegistryEntry }
public enum RiskLevel { Safe, Caution, ManualReview }
public enum PolicyProfile { Conservative, Balanced, Aggressive }
public enum DecisionAction { Remove, Skip, ManualReview, AuditOnly }
public enum ExecutionOutcome { Detected, DryRun, Removed, Skipped, Failed, TimedOut, ManualReview, RebootRequired }

public sealed record DeviceIdentity(string Hostname, string Manufacturer, string Model, string BiosVersion, string SerialNumber, string OsDescription, string OsVersion, string UserName, bool IsAdministrator, bool RebootPending, IReadOnlyList<string> SecurityProducts);

public sealed record InventoryItem(string Id, string Name, string Version, string Publisher, PackageType PackageType, string DetectionMethod, string? UninstallString = null, string? QuietUninstallString = null, string? RegistryPath = null, string? PackageFullName = null);

public sealed class CatalogEntry
{
    public required string Id { get; init; }
    public required string Vendor { get; init; }
    public required string Brand { get; init; }
    public required string ProductPattern { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public PackageType PackageType { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public RiskLevel RiskLevel { get; init; }
    public required string DetectionMethod { get; init; }
    public string? SilentUninstallTemplate { get; init; }
    public string[] KnownDependencies { get; init; } = [];
    public bool RebootRequired { get; init; }
    public bool IsSecurityProduct { get; init; }
    public bool AutomaticRemovalSupported { get; init; } = true;
    public required string Notes { get; init; }
}

public sealed class CleanupPolicy
{
    [JsonConverter(typeof(JsonStringEnumConverter))] public PolicyProfile Profile { get; init; } = PolicyProfile.Conservative;
    public bool DryRun { get; init; } = true;
    public bool Force { get; init; }
    public bool AllowSecurityProductRemoval { get; init; }
    public int ProcessTimeoutSeconds { get; init; } = 900;
    public string[] AllowList { get; init; } = [];
    public string[] BlockList { get; init; } = [];
    public string ReportDirectory { get; init; } = "";
}

public sealed record PolicyDecision(DecisionAction Action, string Reason, bool Forced = false);
public sealed record PlanItem(InventoryItem Inventory, CatalogEntry Catalog, PolicyDecision Decision, int MatchConfidence = 0, IReadOnlyList<string>? MatchRationale = null);
public sealed record ExecutionResult(PlanItem Plan, ExecutionOutcome Outcome, int? ExitCode, string Message, bool RebootRequired, DateTimeOffset Timestamp);

public sealed class AuditReport
{
    public required string SchemaVersion { get; init; }
    public required string ExecutionId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public required string ExecutionMode { get; init; }
    public required PolicyProfile Profile { get; init; }
    public required DeviceIdentity Device { get; init; }
    public required IReadOnlyList<InventoryItem> FullInventory { get; init; }
    public required IReadOnlyList<PlanItem> Before { get; init; }
    public IReadOnlyList<ExecutionResult> Results { get; set; } = [];
    public IReadOnlyList<PlanItem> After { get; set; } = [];
    public IReadOnlyList<InventoryItem> AfterInventory { get; set; } = [];
    public Dictionary<string, int> Summary { get; set; } = [];
    public string? AuditLogPath { get; set; }
    public string? AuditLogSha256 { get; set; }
    public string? ExecutionLogPath { get; set; }
    public string? ExecutionLogSha256 { get; set; }
    public string[] RollbackGuidance { get; set; } = [];
}
