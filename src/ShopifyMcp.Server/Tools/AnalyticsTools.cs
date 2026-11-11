using System.ComponentModel;
using ModelContextProtocol.Server;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Services;

namespace ShopifyMcp.Server.Tools;

[McpServerToolType]
public sealed class AnalyticsTools(AnalyticsService analytics)
{
    [McpServerTool(Name = "sales_report", Title = "Sales report", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Sales totals for the last N days: total, gross and net sales, order count, average order value and items sold. " +
                 "Optionally broken down by day/week/month or by product, variant or sales channel (best sellers first). " +
                 "Amounts are in the store currency. Use this for questions like 'sales in the last 15 days' or 'top products this month'.")]
    public Task<Report> SalesReport(
        [Description("How many days back to include, counting today (1-730). E.g. 15 for the last 15 days.")]
        int days = 30,
        [Description("Breakdown: none, day, week, month, product, variant, sales_channel.")]
        string groupBy = "none",
        [Description("Max rows when grouping by product/variant/channel (1-100).")]
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analytics.SalesReportAsync(days, groupBy, limit, cancellationToken));

    [McpServerTool(Name = "inventory_report", Title = "Inventory value report", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("How much stock is on hand right now: units, cost value (units × recorded unit cost) and retail value. " +
                 "Only inventory-tracked items are counted; items without a recorded cost add 0 to cost value. " +
                 "Optionally grouped by product, variant, vendor or product type, highest value first.")]
    public Task<Report> InventoryReport(
        [Description("Breakdown: none, product, variant, vendor, product_type.")]
        string groupBy = "none",
        [Description("Max rows when grouping (1-100).")]
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analytics.InventoryReportAsync(groupBy, limit, cancellationToken));

    [McpServerTool(Name = "stock_runway", Title = "Stock runway (days left)", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Variants that will run out of stock within N days at their recent sales pace, soonest first. " +
                 "Shows units left, units sold per day and estimated days remaining. Variants with no recent sales are not listed.")]
    public Task<IReadOnlyList<StockRunwayItem>> StockRunway(
        [Description("Return variants with this many days of stock or fewer.")]
        int maxDays = 10,
        [Description("Sales pace is measured over this many past days.")]
        int lookbackDays = 30,
        [Description("Max variants to return (1-250).")]
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analytics.StockRunwayAsync(maxDays, lookbackDays, limit, cancellationToken));

    [McpServerTool(Name = "run_shopifyql", Title = "Run a ShopifyQL query", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Runs any ShopifyQL analytics query for questions the other tools don't cover (returns, discounts, customers, " +
                 "sessions, traffic, comparisons with previous periods...). ShopifyQL is read-only. " +
                 "Shape: FROM <schema> SHOW <metrics> [WHERE ...] [GROUP BY ...] [TIMESERIES day|week|month] [WITH TOTALS] " +
                 "[HAVING ...] [SINCE -30d UNTIL today | DURING last_month] [COMPARE TO previous_period] [ORDER BY ...] [LIMIT n]. " +
                 "Common schemas: sales, orders, customers, inventory, returns, discounts, sessions. " +
                 "Example: FROM sales SHOW net_sales, orders GROUP BY product_title SINCE -7d UNTIL today ORDER BY net_sales DESC LIMIT 5. " +
                 "Reference: https://shopify.dev/docs/api/shopifyql. If the query fails to parse, read the error and fix the field names.")]
    public Task<Report> RunShopifyql(
        [Description("The ShopifyQL query.")]
        string query,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => analytics.RunAsync(query, cancellationToken));
}
