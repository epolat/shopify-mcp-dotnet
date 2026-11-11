using System.Net;
using System.Text;
using System.Text.Json;
using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Core.Alerts;

namespace ShopifyMcp.Tests;

/// <summary>Returns canned HTTP responses in order and records the requests.</summary>
internal sealed class FakeHttpHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
{
    private int _next;
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request, body));
        return responses[Math.Min(_next++, responses.Length - 1)]();
    }

    public static Func<HttpResponseMessage> Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

/// <summary>GraphQL client that answers with prepared <c>data</c> objects and records variables.</summary>
internal sealed class FakeGraphQLClient(params string[] dataJson) : IShopifyGraphQLClient
{
    private int _next;
    public List<(string Query, string Variables)> Calls { get; } = [];

    public Task<JsonElement> ExecuteAsync(string query, object? variables = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((query, JsonSerializer.Serialize(variables)));
        return Task.FromResult(JsonDocument.Parse(dataJson[Math.Min(_next++, dataJson.Length - 1)]).RootElement.Clone());
    }
}

internal sealed class InMemoryAlertStore : IAlertStore
{
    public List<StockAlertRule> Rules { get; } = [];
    public Dictionary<string, DateTimeOffset> Notified { get; } = [];

    public Task<IReadOnlyList<StockAlertRule>> ListRulesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockAlertRule>>(Rules.ToList());

    public Task AddRuleAsync(StockAlertRule rule, CancellationToken ct = default) { Rules.Add(rule); return Task.CompletedTask; }

    public Task<bool> RemoveRuleAsync(string id, CancellationToken ct = default) => Task.FromResult(Rules.RemoveAll(r => r.Id == id) > 0);

    public Task<DateTimeOffset?> LastNotifiedAsync(string ruleId, string itemKey, CancellationToken ct = default) =>
        Task.FromResult(Notified.TryGetValue($"{ruleId}|{itemKey}", out var at) ? at : (DateTimeOffset?)null);

    public Task MarkNotifiedAsync(string ruleId, IEnumerable<string> itemKeys, DateTimeOffset at, CancellationToken ct = default)
    {
        foreach (var k in itemKeys) Notified[$"{ruleId}|{k}"] = at;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingNotifier(string name = "test", bool fail = false) : IAlertNotifier
{
    public string Name => name;
    public List<AlertNotification> Sent { get; } = [];

    public Task SendAsync(AlertNotification notification, CancellationToken ct = default)
    {
        if (fail) throw new InvalidOperationException("boom");
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

internal sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
}
