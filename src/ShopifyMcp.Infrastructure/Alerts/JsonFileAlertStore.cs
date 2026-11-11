using System.Text.Json;
using ShopifyMcp.Core.Alerts;

namespace ShopifyMcp.Infrastructure.Alerts;

/// <summary>
/// Keeps rules and notification history in one JSON file per store, e.g.
/// <c>~/.shopify-mcp/my-store.myshopify.com.alerts.json</c>.
/// The MCP server and the scheduled <c>alerts check</c> run share this file.
/// </summary>
public sealed class JsonFileAlertStore(string path) : IAlertStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Drop history entries older than this so the file doesn't grow forever.
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(60);

    private readonly SemaphoreSlim _lock = new(1, 1);

    public string Path => path;

    public async Task<IReadOnlyList<StockAlertRule>> ListRulesAsync(CancellationToken ct = default) =>
        (await LoadAsync(ct)).Rules;

    public Task AddRuleAsync(StockAlertRule rule, CancellationToken ct = default) =>
        MutateAsync(f => f.Rules.Add(rule), ct);

    public async Task<bool> RemoveRuleAsync(string id, CancellationToken ct = default)
    {
        var removed = false;
        await MutateAsync(f =>
        {
            removed = f.Rules.RemoveAll(r => r.Id == id) > 0;
            foreach (var key in f.Notified.Keys.Where(k => k.StartsWith(id + "|")).ToList())
                f.Notified.Remove(key);
        }, ct);
        return removed;
    }

    public async Task<DateTimeOffset?> LastNotifiedAsync(string ruleId, string itemKey, CancellationToken ct = default) =>
        (await LoadAsync(ct)).Notified.TryGetValue($"{ruleId}|{itemKey}", out var at) ? at : null;

    public Task MarkNotifiedAsync(string ruleId, IEnumerable<string> itemKeys, DateTimeOffset at, CancellationToken ct = default) =>
        MutateAsync(f =>
        {
            foreach (var key in itemKeys)
                f.Notified[$"{ruleId}|{key}"] = at;

            foreach (var stale in f.Notified.Where(kv => at - kv.Value > HistoryRetention).Select(kv => kv.Key).ToList())
                f.Notified.Remove(stale);
        }, ct);

    private async Task<AlertFile> LoadAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try { return await ReadAsync(ct); }
        finally { _lock.Release(); }
    }

    private async Task MutateAsync(Action<AlertFile> change, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var file = await ReadAsync(ct);
            change(file);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(file, Json), ct);
            File.Move(temp, path, overwrite: true); // atomic replace: a crash never leaves half a file
        }
        finally { _lock.Release(); }
    }

    private async Task<AlertFile> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(path))
            return new AlertFile();

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AlertFile>(stream, Json, ct) ?? new AlertFile();
    }

    private sealed class AlertFile
    {
        public List<StockAlertRule> Rules { get; set; } = [];
        public Dictionary<string, DateTimeOffset> Notified { get; set; } = [];
    }
}
