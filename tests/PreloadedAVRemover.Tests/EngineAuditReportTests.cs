using PreloadedAVRemover.Core;
using System.Text.Json;

namespace PreloadedAVRemover.Tests;

public sealed class EngineAuditReportTests
{
    [Fact]
    public async Task DryRun_NeverInvokesProcessRunner()
    {
        var provider = new MockInventoryProvider { Items = [TestData.Msi()] };
        var runner = new FakeRunner(); var engine = new CleanupEngine(provider, new RemovalCatalog([TestData.Entry()]), runner);
        var dir = TestData.TempDirectory(); var log = Path.Combine(dir, "audit.jsonl");
        var report = engine.Audit(new CleanupPolicy(), "execution", log);
        var results = await engine.ExecuteAsync(report.Before, new CleanupPolicy { DryRun = true }, "execution", log);
        Assert.Equal(0, runner.Calls); Assert.All(results, x => Assert.Equal(ExecutionOutcome.DryRun, x.Outcome));
    }

    [Fact]
    public async Task NonAdminRemoval_FailsWithoutExecution()
    {
        var provider = new MockInventoryProvider { Device = TestData.Device(admin: false), Items = [TestData.Msi()] };
        var runner = new FakeRunner(); var engine = new CleanupEngine(provider, new RemovalCatalog([TestData.Entry()]), runner);
        var log = Path.Combine(TestData.TempDirectory(), "audit.jsonl"); var report = engine.Audit(new CleanupPolicy { DryRun = false }, "execution", log);
        var result = Assert.Single(await engine.ExecuteAsync(report.Before, new CleanupPolicy { DryRun = false }, "execution", log));
        Assert.Equal(ExecutionOutcome.Failed, result.Outcome); Assert.Contains("administrator", result.Message); Assert.Equal(0, runner.Calls);
    }

    [Theory]
    [InlineData(1603, ExecutionOutcome.Failed, false)]
    [InlineData(3010, ExecutionOutcome.RebootRequired, true)]
    [InlineData(1641, ExecutionOutcome.RebootRequired, true)]
    [InlineData(0, ExecutionOutcome.Removed, false)]
    public async Task ExitCodes_AreAudited(int exitCode, ExecutionOutcome expected, bool reboot)
    {
        var provider = new MockInventoryProvider { Items = [TestData.Msi()] };
        var runner = new FakeRunner { ExitCode = exitCode }; var engine = new CleanupEngine(provider, new RemovalCatalog([TestData.Entry()]), runner);
        var policy = new CleanupPolicy { DryRun = false }; var log = Path.Combine(TestData.TempDirectory(), "audit.jsonl"); var report = engine.Audit(policy, "execution", log);
        var result = Assert.Single(await engine.ExecuteAsync(report.Before, policy, "execution", log));
        Assert.Equal(expected, result.Outcome); Assert.Equal(reboot, result.RebootRequired); Assert.Equal(exitCode, result.ExitCode);
    }

    [Fact]
    public void EmptyInventory_ModelsMissingRegistryKeysWithoutFailure()
    {
        var provider = new MockInventoryProvider { Items = [] }; var engine = new CleanupEngine(provider, new RemovalCatalog([TestData.Entry()]), new FakeRunner());
        var report = engine.Audit(new CleanupPolicy(), "execution", Path.Combine(TestData.TempDirectory(), "audit.jsonl"));
        Assert.Empty(report.FullInventory); Assert.Empty(report.Before);
    }

    [Fact]
    public void HashChainedLog_ContainsContextAndValidLinkage()
    {
        var path = Path.Combine(TestData.TempDirectory(), "audit.jsonl");
        using (var log = new HashChainAuditLogger(path)) { log.Write("id", "Detection", "one"); log.Write("id", "Decision", "two"); }
        var lines = File.ReadAllLines(path); Assert.Equal(2, lines.Length);
        using var first = JsonDocument.Parse(lines[0]); using var second = JsonDocument.Parse(lines[1]);
        Assert.Equal(first.RootElement.GetProperty("hash").GetString(), second.RootElement.GetProperty("previousHash").GetString());
        Assert.Equal(Environment.MachineName, first.RootElement.GetProperty("hostname").GetString()); Assert.Equal(64, HashChainAuditLogger.Sha256File(path).Length);
    }

    [Fact]
    public void ReportWriter_ProducesMachineReadableJsonAndEscapedHtml()
    {
        var provider = new MockInventoryProvider { Items = [TestData.Msi("<script>alert(1)</script>")] };
        var engine = new CleanupEngine(provider, new RemovalCatalog([TestData.Entry(pattern: ".*")]), new FakeRunner());
        var dir = TestData.TempDirectory(); var report = engine.Audit(new CleanupPolicy(), "1234567890abcdef", Path.Combine(dir, "audit.jsonl"));
        report.Results = report.Before.Select(x => new ExecutionResult(x, ExecutionOutcome.Detected, null, "detected", false, DateTimeOffset.UtcNow)).ToList(); report.After = report.Before; report.AfterInventory = report.FullInventory;
        var paths = ReportWriter.Write(report, dir);
        Assert.True(File.Exists(paths.Json)); Assert.True(File.Exists(paths.Html));
        using var json = JsonDocument.Parse(File.ReadAllText(paths.Json)); Assert.Equal("2.0", json.RootElement.GetProperty("schemaVersion").GetString());
        var html = File.ReadAllText(paths.Html); Assert.DoesNotContain("<script>alert", html); Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void WindowsInventoryProvider_ReadOnlyIntegration_DoesNotThrow()
    {
        var provider = new WindowsInventoryProvider();
        var device = provider.GetDeviceIdentity(); var inventory = provider.GetInventory();
        Assert.False(string.IsNullOrWhiteSpace(device.Hostname)); Assert.NotNull(inventory);
    }
}
