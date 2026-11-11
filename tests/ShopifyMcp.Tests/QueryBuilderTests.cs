using ShopifyMcp.Core.Services;
using ShopifyMcp.Infrastructure;

namespace ShopifyMcp.Tests;

public class OrderSearchQueryTests
{
    [Fact]
    public void Build_returns_null_without_filters() =>
        Assert.Null(OrderSearchQuery.Build(new OrderFilter()));

    [Fact]
    public void Build_combines_filters_with_AND()
    {
        var query = OrderSearchQuery.Build(new OrderFilter(
            Status: "Open",
            FulfillmentStatus: "unfulfilled",
            CreatedAfter: new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.FromHours(3)),
            Text: "tag:wholesale"));

        Assert.Equal(
            "status:open AND fulfillment_status:unfulfilled AND created_at:>='2026-11-01T00:00:00Z' AND tag:wholesale",
            query);
    }

    [Fact]
    public void Build_treats_any_as_no_filter() =>
        Assert.Null(OrderSearchQuery.Build(new OrderFilter(Status: "any")));

    [Fact]
    public void Build_rejects_unknown_status()
    {
        var ex = Assert.Throws<ArgumentException>(() => OrderSearchQuery.Build(new OrderFilter(FinancialStatus: "free")));
        Assert.Contains("partially_refunded", ex.Message);
    }

    [Theory]
    [InlineData("1042", null, "name:'#1042'")]
    [InlineData("#1042", null, "name:'#1042'")]
    [InlineData(" gid://shopify/Order/123 ", "gid://shopify/Order/123", null)]
    public void ParseOrderReference_handles_numbers_and_gids(string input, string? gid, string? nameQuery) =>
        Assert.Equal((gid, nameQuery), OrderSearchQuery.ParseOrderReference(input));
}

public class AnalyticsQueryTests
{
    [Fact]
    public void Sales_query_for_last_15_days_without_grouping() =>
        Assert.Equal(
            "FROM sales SHOW total_sales, gross_sales, net_sales, orders, average_order_value, net_items_sold SINCE -15d UNTIL today",
            AnalyticsService.BuildSalesQuery(15, "none", 20));

    [Fact]
    public void Sales_query_by_day_is_a_timeseries() =>
        Assert.Equal(
            "FROM sales SHOW total_sales, gross_sales, net_sales, orders, average_order_value, net_items_sold " +
            "TIMESERIES day WITH TOTALS SINCE -7d UNTIL today ORDER BY day ASC",
            AnalyticsService.BuildSalesQuery(7, "DAY", 20));

    [Fact]
    public void Sales_query_by_product_ranks_best_sellers() =>
        Assert.EndsWith(
            "GROUP BY product_title WITH TOTALS SINCE -30d UNTIL today ORDER BY net_sales DESC LIMIT 5",
            AnalyticsService.BuildSalesQuery(30, "product", 5));

    [Fact]
    public void Sales_query_rejects_bad_input()
    {
        Assert.Throws<ArgumentException>(() => AnalyticsService.BuildSalesQuery(0, "none", 10));
        Assert.Throws<ArgumentException>(() => AnalyticsService.BuildSalesQuery(10, "color", 10));
    }

    [Fact]
    public void Inventory_query_counts_tracked_items_only() =>
        Assert.Equal(
            "FROM inventory SHOW ending_inventory_units, ending_inventory_value, ending_inventory_retail_value " +
            "WHERE inventory_is_tracked = true SINCE -1d UNTIL today",
            AnalyticsService.BuildInventoryQuery("none", 20));

    [Fact]
    public void Runway_query_filters_with_having() =>
        Assert.Equal(
            "FROM inventory SHOW ending_inventory_units, inventory_units_sold_per_day, days_of_inventory_remaining " +
            "WHERE inventory_is_tracked = true GROUP BY product_title, product_variant_title, product_variant_sku " +
            "HAVING days_of_inventory_remaining <= 10 SINCE -30d UNTIL today ORDER BY days_of_inventory_remaining ASC LIMIT 50",
            AnalyticsService.BuildRunwayQuery(10, 30, 50));
}

public class ShopifyOptionsTests
{
    [Theory]
    [InlineData("my-store", "my-store.myshopify.com")]
    [InlineData("My-Store.myshopify.com", "my-store.myshopify.com")]
    [InlineData("https://my-store.myshopify.com/", "my-store.myshopify.com")]
    [InlineData("  ", "")]
    public void NormalizeDomain(string input, string expected) =>
        Assert.Equal(expected, ShopifyOptions.NormalizeDomain(input));

    [Fact]
    public void Validate_requires_some_credentials()
    {
        var options = new ShopifyOptions { StoreDomain = "x.myshopify.com", ClientId = "id" };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
