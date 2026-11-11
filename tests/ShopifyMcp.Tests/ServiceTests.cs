using ShopifyMcp.Core;
using ShopifyMcp.Core.Alerts;
using ShopifyMcp.Core.Services;

namespace ShopifyMcp.Tests;

public class OrderServiceTests
{
    private const string OrderJson = """
        { "order": {
            "id": "gid://shopify/Order/1", "name": "#1042", "createdAt": "2026-11-10T10:00:00Z",
            "note": "first", "email": "a@example.com",
            "displayFinancialStatus": "PAID", "displayFulfillmentStatus": "UNFULFILLED",
            "totalPriceSet": { "shopMoney": { "amount": "149.90", "currencyCode": "TRY" } },
            "customer": { "displayName": "Ada" }, "shippingAddress": null,
            "lineItems": { "nodes": [ { "title": "Mug", "quantity": 2, "sku": "MUG-1",
              "originalUnitPriceSet": { "shopMoney": { "amount": "74.95", "currencyCode": "TRY" } } } ] } } }
        """;

    [Fact]
    public async Task GetAsync_resolves_order_number_then_maps_details()
    {
        var client = new FakeGraphQLClient("""{ "orders": { "nodes": [ { "id": "gid://shopify/Order/1" } ] } }""", OrderJson);

        var order = await new OrderService(client).GetAsync("1042");

        using var variables = System.Text.Json.JsonDocument.Parse(client.Calls[0].Variables);
        Assert.Equal("name:'#1042'", variables.RootElement.GetProperty("query").GetString());
        Assert.Equal("#1042", order.Name);
        Assert.Equal(149.90m, order.Total!.Amount);
        Assert.Equal("MUG-1", Assert.Single(order.LineItems).Sku);
    }

    [Fact]
    public async Task GetAsync_reports_missing_order()
    {
        var client = new FakeGraphQLClient("""{ "orders": { "nodes": [] } }""");
        var ex = await Assert.ThrowsAsync<ShopifyException>(() => new OrderService(client).GetAsync("#9999"));
        Assert.Contains("#9999", ex.Message);
    }

    [Fact]
    public async Task UpdateNoteAsync_appends_to_existing_note()
    {
        var client = new FakeGraphQLClient(
            OrderJson,
            """{ "orderUpdate": { "order": { "id": "gid://shopify/Order/1", "name": "#1042", "note": "first\nsecond" }, "userErrors": [] } }""");

        await new OrderService(client).UpdateNoteAsync("gid://shopify/Order/1", "second", append: true);

        using var variables = System.Text.Json.JsonDocument.Parse(client.Calls[1].Variables);
        Assert.Equal("first" + Environment.NewLine + "second",
            variables.RootElement.GetProperty("input").GetProperty("note").GetString());
    }

    [Fact]
    public async Task UpdateNoteAsync_surfaces_user_errors()
    {
        var client = new FakeGraphQLClient(
            """{ "orderUpdate": { "order": null, "userErrors": [ { "field": ["note"], "message": "Note is too long" } ] } }""");

        var ex = await Assert.ThrowsAsync<ShopifyException>(() =>
            new OrderService(client).UpdateNoteAsync("gid://shopify/Order/1", "x", append: false));
        Assert.Contains("Note is too long", ex.Message);
    }
}

public class AnalyticsServiceTests
{
    [Fact]
    public async Task RunAsync_turns_parse_errors_into_a_readable_exception()
    {
        var client = new FakeGraphQLClient("""{ "shopifyqlQuery": { "tableData": null, "parseErrors": ["Column 'total_sale' not found"] } }""");

        var ex = await Assert.ThrowsAsync<ShopifyException>(() => new AnalyticsService(client).RunAsync("FROM sales SHOW total_sale"));
        Assert.Contains("total_sale", ex.Message);
    }

    [Fact]
    public async Task StockRunwayAsync_parses_numbers_sent_as_strings()
    {
        var client = new FakeGraphQLClient("""
            { "shopifyqlQuery": { "parseErrors": [], "tableData": {
                "columns": [ { "name": "product_title", "dataType": "STRING", "displayName": "Product" } ],
                "rows": [ { "product_title": "Mug", "product_variant_title": "Blue", "product_variant_sku": "MUG-B",
                            "ending_inventory_units": 12, "inventory_units_sold_per_day": "2.5",
                            "days_of_inventory_remaining": "4.8" } ] } } }
            """);

        var item = Assert.Single(await new AnalyticsService(client).StockRunwayAsync(10, 30, 50));

        Assert.Equal(12m, item.UnitsLeft);
        Assert.Equal(2.5m, item.UnitsSoldPerDay);
        Assert.Equal(4.8m, item.DaysRemaining);
    }
}

public class AlertServiceTests
{
    private const string OneLowItem = """
        { "shopifyqlQuery": { "parseErrors": [], "tableData": { "columns": [], "rows": [
            { "product_title": "Mug", "product_variant_title": "Blue", "product_variant_sku": "MUG-B",
              "ending_inventory_units": 5, "inventory_units_sold_per_day": 1, "days_of_inventory_remaining": 5 } ] } } }
        """;

    private static (AlertService Service, InMemoryAlertStore Store, ManualTime Time) Create(params IAlertNotifier[] notifiers)
    {
        var store = new InMemoryAlertStore();
        var time = new ManualTime(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero));
        var service = new AlertService(
            new AnalyticsService(new FakeGraphQLClient(OneLowItem)), store, notifiers,
            new AlertSettings("my-store.myshopify.com", TimeSpan.FromHours(24)), time);
        return (service, store, time);
    }

    [Fact]
    public async Task Notifies_once_then_waits_for_cooldown()
    {
        var notifier = new RecordingNotifier();
        var (service, _, time) = Create(notifier);
        await service.AddStockRuleAsync(10, 30, null);

        await service.CheckAsync(notify: true);
        time.Now = time.Now.AddHours(1);
        var second = await service.CheckAsync(notify: true);
        time.Now = time.Now.AddHours(24);
        await service.CheckAsync(notify: true);

        Assert.Equal(2, notifier.Sent.Count);
        Assert.Single(second[0].Triggered);
        Assert.Empty(second[0].NewSinceLastNotification);
        Assert.Contains("Mug / Blue [MUG-B]: 5 days left", notifier.Sent[0].ToText());
    }

    [Fact]
    public async Task A_failing_channel_does_not_block_the_others()
    {
        var good = new RecordingNotifier("good");
        var (service, store, _) = Create(new RecordingNotifier("bad", fail: true), good);
        await service.AddStockRuleAsync(10, 30, "low stock");

        var result = Assert.Single(await service.CheckAsync(notify: true));

        Assert.Single(good.Sent);
        Assert.Equal(["good"], result.NotifiedChannels);
        Assert.Contains("bad: boom", Assert.Single(result.Errors));
        Assert.Single(store.Notified);
    }

    [Fact]
    public async Task Check_without_notify_sends_nothing()
    {
        var notifier = new RecordingNotifier();
        var (service, store, _) = Create(notifier);
        await service.AddStockRuleAsync(10, 30, null);

        await service.CheckAsync(notify: false);

        Assert.Empty(notifier.Sent);
        Assert.Empty(store.Notified);
    }

    [Fact]
    public async Task Rejects_invalid_rule() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Create().Service.AddStockRuleAsync(0, 30, null));
}
