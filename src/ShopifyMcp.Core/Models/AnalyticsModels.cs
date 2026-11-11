using System.Text.Json;

namespace ShopifyMcp.Core.Models;

public sealed record ReportColumn(string Name, string DataType, string? DisplayName);

/// <summary>
/// A ShopifyQL result. <see cref="Query"/> is included so the assistant can show
/// or tweak the query it ran.
/// </summary>
public sealed record Report(string Query, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<JsonElement> Rows);

/// <summary>A variant that will run out of stock soon at its recent sales pace.</summary>
public sealed record StockRunwayItem(
    string ProductTitle,
    string? VariantTitle,
    string? Sku,
    decimal? UnitsLeft,
    decimal? UnitsSoldPerDay,
    decimal? DaysRemaining)
{
    /// <summary>Stable key used to avoid notifying about the same variant again and again.</summary>
    public string Key => $"{ProductTitle}|{VariantTitle}|{Sku}";
}
