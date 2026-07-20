using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PreloadedAVRemover.Core;

public sealed class HashChainAuditLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private string _previousHash = new('0', 64);
    public string Path { get; }

    public HashChainAuditLogger(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Write(string executionId, string category, string message, object? data = null)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var dataJson = JsonSerializer.Serialize(data);
        var material = $"{_previousHash}|{timestamp:O}|{Environment.MachineName}|{Environment.UserName}|{executionId}|{category}|{message}|{dataJson}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        _writer.WriteLine(JsonSerializer.Serialize(new { timestamp, hostname = Environment.MachineName, user = Environment.UserName, executionId, category, message, data, previousHash = _previousHash, hash }));
        _previousHash = hash;
    }

    public void Dispose() => _writer.Dispose();
    public static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static (string Json, string Html) Write(AuditReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        var stem = $"OEM-Cleanup-{Sanitize(report.Device.Hostname)}-{report.StartedAt:yyyyMMdd-HHmmss}-{report.ExecutionId[..8]}";
        var jsonPath = Path.Combine(directory, stem + ".json");
        var htmlPath = Path.Combine(directory, stem + ".html");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false));
        File.WriteAllText(htmlPath, BuildHtml(report), new UTF8Encoding(false));
        return (jsonPath, htmlPath);
    }

    private static string BuildHtml(AuditReport r)
    {
        static string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");
        var rows = string.Join("", r.Results.Select(x => $"<tr><td>{E(x.Plan.Inventory.Name)}</td><td>{E(x.Plan.Catalog.Brand)}</td><td>{x.Plan.Catalog.RiskLevel}</td><td>{x.Outcome}</td><td>{E(x.Message)}</td><td>{x.ExitCode}</td></tr>"));
        var inventory = string.Join("", r.FullInventory.Select(x => $"<tr><td>{E(x.Name)}</td><td>{E(x.Version)}</td><td>{E(x.Publisher)}</td><td>{x.PackageType}</td><td>{E(x.DetectionMethod)}</td></tr>"));
        return $$"""
        <!doctype html><html><head><meta charset="utf-8"><title>OEM Cleanup Audit</title>
        <style>body{font:14px Segoe UI,Arial;margin:32px;color:#1e293b}h1{color:#0f172a}.cards{display:flex;gap:14px}.card{padding:14px 18px;background:#f1f5f9;border-radius:8px}table{width:100%;border-collapse:collapse;margin:12px 0 28px}th,td{text-align:left;padding:8px;border-bottom:1px solid #e2e8f0}th{background:#f8fafc}.mono{font-family:Consolas,monospace}</style></head><body>
        <h1>OEM Cleanup Audit</h1><p class="mono">Execution {{E(r.ExecutionId)}} | {{E(r.ExecutionMode)}} | {{r.StartedAt:O}}</p>
        <div class="cards"><div class="card"><b>Device</b><br>{{E(r.Device.Manufacturer)}} {{E(r.Device.Model)}}</div><div class="card"><b>Hostname</b><br>{{E(r.Device.Hostname)}}</div><div class="card"><b>Matched before / after</b><br>{{r.Before.Count}} / {{r.After.Count}}</div><div class="card"><b>Inventory before / after</b><br>{{r.FullInventory.Count}} / {{r.AfterInventory.Count}}</div></div>
        <h2>Execution results</h2><table><tr><th>Product</th><th>Brand</th><th>Risk</th><th>Outcome</th><th>Decision</th><th>Exit</th></tr>{{rows}}</table>
        <h2>Full installed software inventory</h2><table><tr><th>Name</th><th>Version</th><th>Publisher</th><th>Type</th><th>Detection</th></tr>{{inventory}}</table>
        <p>Audit log SHA-256: <span class="mono">{{E(r.AuditLogSha256)}}</span><br>Execution log SHA-256: <span class="mono">{{E(r.ExecutionLogSha256)}}</span></p></body></html>
        """;
    }
    private static string Sanitize(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
}
