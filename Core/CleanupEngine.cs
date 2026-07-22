namespace PreloadedAVRemover.Core;

public interface IInventoryProvider
{
    DeviceIdentity GetDeviceIdentity();
    IReadOnlyList<InventoryItem> GetInventory();
}

public sealed class CleanupEngine
{
    private readonly IInventoryProvider _inventory;
    private readonly RemovalCatalog _catalog;
    private readonly IProcessRunner _runner;

    public CleanupEngine(IInventoryProvider inventory, RemovalCatalog catalog, IProcessRunner runner) { _inventory = inventory; _catalog = catalog; _runner = runner; }

    public AuditReport Audit(CleanupPolicy policy, string executionId, string logPath)
    {
        var device = _inventory.GetDeviceIdentity();
        var all = _inventory.GetInventory();
        var plan = _catalog.Match(all, device, policy);
        using var log = new HashChainAuditLogger(logPath);
        log.Write(executionId, "Start", "Audit started", new { policy.Profile, policy.DryRun, policy.Force, policy.AllowSecurityProductRemoval, device });
        foreach (var item in all) log.Write(executionId, "Inventory", "Installed software detected", item);
        foreach (var item in plan) log.Write(executionId, "Decision", item.Decision.Reason, item);
        return new AuditReport { SchemaVersion = "2.2", ExecutionId = executionId, StartedAt = DateTimeOffset.UtcNow, ExecutionMode = policy.DryRun ? "DryRun" : "Removal", Profile = policy.Profile, Device = device, FullInventory = all, Before = plan, AuditLogPath = logPath };
    }

    public async Task<IReadOnlyList<ExecutionResult>> ExecuteAsync(IReadOnlyList<PlanItem> plan, CleanupPolicy policy, string executionId, string logPath, CancellationToken cancellationToken = default)
    {
        var results = new List<ExecutionResult>();
        using var log = new HashChainAuditLogger(UniqueContinuationPath(logPath));
        var isAdmin = _inventory.GetDeviceIdentity().IsAdministrator;
        log.Write(executionId, "ExecutionStart", "Execution pass started", new { policy.DryRun, policy.Force, policy.Profile, policy.AllowSecurityProductRemoval, isAdmin });
        foreach (var item in plan)
        {
            ExecutionResult result;
            if (item.Decision.Action != DecisionAction.Remove)
            {
                var outcome = item.Decision.Action == DecisionAction.ManualReview ? ExecutionOutcome.ManualReview : ExecutionOutcome.Skipped;
                result = new(item, outcome, null, item.Decision.Reason, false, DateTimeOffset.UtcNow);
            }
            else if (policy.DryRun)
            {
                var validation = CommandValidator.Validate(item, policy.ProcessTimeoutSeconds);
                result = new(item, ExecutionOutcome.DryRun, null, validation.IsValid ? "Dry-run: validated command; no changes made" : $"Dry-run: command rejected - {validation.Reason}", false, DateTimeOffset.UtcNow);
            }
            else if (!isAdmin)
            {
                result = new(item, ExecutionOutcome.Failed, null, "Removal requires an elevated administrator context", false, DateTimeOffset.UtcNow);
            }
            else
            {
                var validation = CommandValidator.Validate(item, policy.ProcessTimeoutSeconds);
                if (!validation.IsValid || validation.Command is null) result = new(item, ExecutionOutcome.Failed, null, validation.Reason, false, DateTimeOffset.UtcNow);
                else
                {
                    try
                    {
                        log.Write(executionId, "Command", "Executing validated command", new { validation.Command.FileName, validation.Command.Arguments, validation.Command.Source, validation.Command.TimeoutSeconds });
                        var processResult = await _runner.RunAsync(validation.Command, cancellationToken);
                        var exitCode = processResult.ExitCode;
                        var reboot = exitCode is 3010 or 1641 || item.Catalog.RebootRequired;
                        var outcome = exitCode is 0 or 3010 or 1641 ? (reboot ? ExecutionOutcome.RebootRequired : ExecutionOutcome.Removed) : ExecutionOutcome.Failed;
                        var message = outcome == ExecutionOutcome.Failed
                            ? "Uninstaller returned a failure exit code" + (string.IsNullOrWhiteSpace(processResult.DiagnosticOutput) ? string.Empty : $": {processResult.DiagnosticOutput}")
                            : "Validated uninstaller completed";
                        result = new(item, outcome, exitCode, message, reboot, DateTimeOffset.UtcNow);
                    }
                    catch (ProcessTimeoutException ex) { result = new(item, ExecutionOutcome.TimedOut, null, ex.Message, false, DateTimeOffset.UtcNow); }
                    catch (Exception ex) { result = new(item, ExecutionOutcome.Failed, null, ex.Message, false, DateTimeOffset.UtcNow); }
                }
            }
            results.Add(result);
            log.Write(executionId, "Result", result.Message, result);
        }
        var afterInventory = _inventory.GetInventory();
        foreach (var item in afterInventory) log.Write(executionId, "PostInventory", "Installed software detected after execution pass", item);
        foreach (var item in _catalog.Match(afterInventory, _inventory.GetDeviceIdentity(), policy)) log.Write(executionId, "PostDecision", item.Decision.Reason, item);
        return results;
    }

    public IReadOnlyList<PlanItem> Rescan(CleanupPolicy policy) => _catalog.Match(_inventory.GetInventory(), _inventory.GetDeviceIdentity(), policy);
    public (IReadOnlyList<InventoryItem> Inventory, IReadOnlyList<PlanItem> Plan) RescanFull(CleanupPolicy policy)
    {
        var inventory = _inventory.GetInventory();
        return (inventory, _catalog.Match(inventory, _inventory.GetDeviceIdentity(), policy));
    }
    public static string ExecutionLogPath(string path) => Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-execution" + Path.GetExtension(path));
    private static string UniqueContinuationPath(string path) => ExecutionLogPath(path);
}
