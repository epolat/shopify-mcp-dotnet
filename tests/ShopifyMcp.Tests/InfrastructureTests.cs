using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ShopifyMcp.Core;
using ShopifyMcp.Core.Alerts;
using ShopifyMcp.Infrastructure;
using ShopifyMcp.Infrastructure.Alerts;
using ShopifyMcp.Infrastructure.Auth;

namespace ShopifyMcp.Tests;

public class ShopifyGraphQLClientTests
{
    private static readonly ShopifyOptions Options = new() { StoreDomain = "my-store.myshopify.com", AccessToken = "shpat_test" };

    private static ShopifyGraphQLClient Create(FakeHttpHandler handler, IAccessTokenProvider? tokens = null) =>
        new(new HttpClient(handler), Options, tokens ?? new StaticAccessTokenProvider("shpat_test"),
            NullLogger<ShopifyGraphQLClient>.Instance, (_, _) => Task.CompletedTask);

    [Fact]
    public async Task Sends_token_to_versioned_endpoint_and_returns_data()
    {
        var handler = new FakeHttpHandler(FakeHttpHandler.Json("""{ "data": { "shop": { "name": "Demo" } } }"""));

        var data = await Create(handler).ExecuteAsync("query { shop { name } }");

        var (request, _) = Assert.Single(handler.Requests);
        Assert.Equal("https://my-store.myshopify.com/admin/api/2026-07/graphql.json", request.RequestUri!.ToString());
        Assert.Equal("shpat_test", request.Headers.GetValues("X-Shopify-Access-Token").Single());
        Assert.Equal("Demo", data.GetProperty("shop").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Retries_when_throttled()
    {
        var handler = new FakeHttpHandler(
            FakeHttpHandler.Json("""{ "errors": [ { "message": "Throttled", "extensions": { "code": "THROTTLED" } } ] }"""),
            FakeHttpHandler.Json("""{ "data": { "ok": true } }"""));

        var data = await Create(handler).ExecuteAsync("query { ok }");

        Assert.Equal(2, handler.Requests.Count);
        Assert.True(data.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Gives_up_after_max_attempts()
    {
        var handler = new FakeHttpHandler(FakeHttpHandler.Json("", HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<ShopifyException>(() => Create(handler).ExecuteAsync("query { ok }"));
        Assert.Equal(ShopifyGraphQLClient.MaxAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task Maps_graphql_errors_to_ShopifyException()
    {
        var handler = new FakeHttpHandler(FakeHttpHandler.Json("""{ "errors": [ { "message": "Field 'x' doesn't exist" } ] }"""));

        var ex = await Assert.ThrowsAsync<ShopifyException>(() => Create(handler).ExecuteAsync("query { x }"));
        Assert.Contains("Field 'x' doesn't exist", ex.Message);
    }

    [Fact]
    public async Task Explains_missing_scope_on_403()
    {
        var handler = new FakeHttpHandler(FakeHttpHandler.Json("{}", HttpStatusCode.Forbidden));

        var ex = await Assert.ThrowsAsync<ShopifyException>(() => Create(handler).ExecuteAsync("query { x }"));
        Assert.Contains("scope", ex.Message);
    }
}

public class ClientCredentialsTokenProviderTests
{
    private static readonly ShopifyOptions Options = new()
    {
        StoreDomain = "my-store.myshopify.com", ClientId = "cid", ClientSecret = "secret",
    };

    [Fact]
    public async Task Requests_token_with_client_credentials_and_caches_it()
    {
        var handler = new FakeHttpHandler(FakeHttpHandler.Json("""{ "access_token": "tok1", "scope": "read_orders", "expires_in": 86399 }"""));
        var provider = new ClientCredentialsTokenProvider(new HttpClient(handler), Options);

        Assert.Equal("tok1", await provider.GetTokenAsync());
        Assert.Equal("tok1", await provider.GetTokenAsync());

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://my-store.myshopify.com/admin/oauth/access_token", request.RequestUri!.ToString());
        Assert.Equal("grant_type=client_credentials&client_id=cid&client_secret=secret", body);
    }

    [Fact]
    public async Task Refreshes_shortly_before_expiry()
    {
        var handler = new FakeHttpHandler(
            FakeHttpHandler.Json("""{ "access_token": "tok1", "expires_in": 86399 }"""),
            FakeHttpHandler.Json("""{ "access_token": "tok2", "expires_in": 86399 }"""));
        var time = new ManualTime(DateTimeOffset.UnixEpoch);
        var provider = new ClientCredentialsTokenProvider(new HttpClient(handler), Options, time);

        await provider.GetTokenAsync();
        time.Now = time.Now.AddSeconds(86399 - 30);

        Assert.Equal("tok2", await provider.GetTokenAsync());
    }

    [Fact]
    public async Task Explains_failed_token_request()
    {
        var handler = new FakeHttpHandler(FakeHttpHandler.Json("""{ "error": "shop_not_permitted" }""", HttpStatusCode.BadRequest));
        var provider = new ClientCredentialsTokenProvider(new HttpClient(handler), Options);

        var ex = await Assert.ThrowsAsync<ShopifyException>(() => provider.GetTokenAsync());
        Assert.Contains("shop_not_permitted", ex.Message);
    }
}

public class JsonFileAlertStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "shopify-mcp-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Persists_rules_and_history_across_instances()
    {
        var path = Path.Combine(_dir, "store.alerts.json");
        var rule = new StockAlertRule("r1", "low", 10, 30, DateTimeOffset.UnixEpoch);
        var at = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);

        var first = new JsonFileAlertStore(path);
        await first.AddRuleAsync(rule);
        await first.MarkNotifiedAsync("r1", ["Mug|Blue|MUG-B"], at);

        var second = new JsonFileAlertStore(path);
        Assert.Equal(rule, Assert.Single(await second.ListRulesAsync()));
        Assert.Equal(at, await second.LastNotifiedAsync("r1", "Mug|Blue|MUG-B"));

        Assert.True(await second.RemoveRuleAsync("r1"));
        Assert.Empty(await second.ListRulesAsync());
        Assert.Null(await second.LastNotifiedAsync("r1", "Mug|Blue|MUG-B"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
