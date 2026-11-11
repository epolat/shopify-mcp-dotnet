using System.Globalization;
using System.Text.Json;
using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Queries;

namespace ShopifyMcp.Core.Services;

/// <summary>
/// Sales and inventory reports built on ShopifyQL, Shopify's analytics query language.
/// https://shopify.dev/docs/api/shopifyql
/// </summary>
public sealed class AnalyticsService(IShopifyGraphQLClient client)
{
    public static readonly IReadOnlyList<string> SalesGroupings =
        ["none", "day", "week", "month", "product", "variant", "sales_channel"];

    public static readonly IReadOnlyList<string> InventoryGroupings =
        ["none", "product", "variant", "vendor", "product_type"];

    private const string SalesMetrics = "total_sales, gross_sales, net_sales, orders, average_order_value, net_items_sold";
    private const string InventoryMetrics = "ending_inventory_units, ending_inventory_value, ending_inventory_retail_value";

    /// <summary>Runs any ShopifyQL query (read-only by design).</summary>
    public async Task<Report> RunAsync(string shopifyql, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shopifyql))
            throw new ArgumentException("The ShopifyQL query is empty.");

        var query = shopifyql.Trim();
        var data = await client.ExecuteAsync(ShopifyQueries.RunShopifyql, new { query }, ct);
        var result = Wire.Read<ShopifyqlResponse>(data, "shopifyqlQuery");

        if (result.ParseErrors is { Count: > 0 } errors)
            throw new ShopifyException(
                $"ShopifyQL could not parse the query: {string.Join("; ", errors)}. Query was: {query}");

        var table = result.TableData;
        return new Report(
            query,
            table?.Columns.Select(c => new ReportColumn(c.Name, c.DataType, c.DisplayName)).ToList() ?? [],
            table?.Rows ?? []);
    }

    public static string BuildSalesQuery(int days, string groupBy, int limit)
    {
        var range = $"SINCE -{CheckDays(days)}d UNTIL today";
        var top = Math.Clamp(limit, 1, 100);

        return Check(groupBy, SalesGroupings) switch
        {
            "none" => $"FROM sales SHOW {SalesMetrics} {range}",
            var g when g is "day" or "week" or "month" =>
                $"FROM sales SHOW {SalesMetrics} TIMESERIES {g} WITH TOTALS {range} ORDER BY {g} ASC",
            "product" => Grouped("product_title"),
            "variant" => Grouped("product_title, product_variant_title, product_variant_sku"),
            "sales_channel" => Grouped("sales_channel"),
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy)),
        };

        string Grouped(string dims) =>
            $"FROM sales SHOW {SalesMetrics} GROUP BY {dims} WITH TOTALS {range} ORDER BY net_sales DESC LIMIT {top}";
    }

    public static string BuildInventoryQuery(string groupBy, int limit)
    {
        const string where = "WHERE inventory_is_tracked = true";
        const string range = "SINCE -1d UNTIL today";
        var top = Math.Clamp(limit, 1, 100);

        return Check(groupBy, InventoryGroupings) switch
        {
            "none" => $"FROM inventory SHOW {InventoryMetrics} {where} {range}",
            "product" => Grouped("product_title"),
            "variant" => Grouped("product_title, product_variant_title, product_variant_sku"),
            "vendor" => Grouped("product_vendor"),
            "product_type" => Grouped("product_type"),
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy)),
        };

        string Grouped(string dims) =>
            $"FROM inventory SHOW {InventoryMetrics} {where} GROUP BY {dims} WITH TOTALS {range} " +
            $"ORDER BY ending_inventory_value DESC LIMIT {top}";
    }

    /// <summary>
    /// Variants that will run out within <paramref name="maxDays"/> days at the sales pace of the
    /// last <paramref name="lookbackDays"/> days. Variants with no sales in that window are not included.
    /// </summary>
    public static string BuildRunwayQuery(int maxDays, int lookbackDays, int limit) =>
        "FROM inventory SHOW ending_inventory_units, inventory_units_sold_per_day, days_of_inventory_remaining " +
        "WHERE inventory_is_tracked = true " +
        "GROUP BY product_title, product_variant_title, product_variant_sku " +
        $"HAVING days_of_inventory_remaining <= {Math.Max(0, maxDays)} " +
        $"SINCE -{CheckDays(lookbackDays)}d UNTIL today " +
        $"ORDER BY days_of_inventory_remaining ASC LIMIT {Math.Clamp(limit, 1, 250)}";

    public Task<Report> SalesReportAsync(int days, string groupBy, int limit, CancellationToken ct = default) =>
        RunAsync(BuildSalesQuery(days, groupBy, limit), ct);

    public Task<Report> InventoryReportAsync(string groupBy, int limit, CancellationToken ct = default) =>
        RunAsync(BuildInventoryQuery(groupBy, limit), ct);

    public async Task<IReadOnlyList<StockRunwayItem>> StockRunwayAsync(
        int maxDays, int lookbackDays, int limit, CancellationToken ct = default)
    {
        var report = await RunAsync(BuildRunwayQuery(maxDays, lookbackDays, limit), ct);
        return report.Rows.Select(r => new StockRunwayItem(
            Text(r, "product_title") ?? "(untitled)",
            Text(r, "product_variant_title"),
            Text(r, "product_variant_sku"),
            Number(r, "ending_inventory_units"),
            Number(r, "inventory_units_sold_per_day"),
            Number(r, "days_of_inventory_remaining"))).ToList();
    }

    private static int CheckDays(int days) => days is >= 1 and <= 730
        ? days
        : throw new ArgumentException("Days must be between 1 and 730.");

    private static string Check(string value, IReadOnlyList<string> allowed)
    {
        var normalized = (value ?? "none").Trim().ToLowerInvariant();
        return allowed.Contains(normalized)
            ? normalized
            : throw new ArgumentException($"Invalid groupBy '{value}'. Allowed: {string.Join(", ", allowed)}.");
    }

    // ShopifyQL returns numbers either as JSON numbers or as strings like "123.45".
    internal static decimal? Number(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    internal static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
}
