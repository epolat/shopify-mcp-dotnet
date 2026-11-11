using System.Globalization;

namespace ShopifyMcp.Core.Services;

/// <summary>
/// Filters for listing orders. Translated into Shopify's search syntax
/// (https://shopify.dev/docs/api/usage/search-syntax).
/// </summary>
public sealed record OrderFilter(
    string? Status = null,
    string? FulfillmentStatus = null,
    string? FinancialStatus = null,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    string? Text = null);

public static class OrderSearchQuery
{
    public static readonly IReadOnlyList<string> Statuses = ["open", "closed", "cancelled", "not_closed"];

    public static readonly IReadOnlyList<string> FulfillmentStatuses =
        ["unshipped", "shipped", "fulfilled", "partial", "scheduled", "on_hold", "unfulfilled", "request_declined"];

    public static readonly IReadOnlyList<string> FinancialStatuses =
        ["paid", "pending", "authorized", "partially_paid", "partially_refunded", "refunded", "voided", "expired"];

    /// <summary>Builds the search string, or returns null when no filter is set.</summary>
    /// <exception cref="ArgumentException">A status value is not one Shopify accepts.</exception>
    public static string? Build(OrderFilter filter)
    {
        var parts = new List<string>();

        AddEnum(parts, "status", filter.Status, Statuses);
        AddEnum(parts, "fulfillment_status", filter.FulfillmentStatus, FulfillmentStatuses);
        AddEnum(parts, "financial_status", filter.FinancialStatus, FinancialStatuses);

        if (filter.CreatedAfter is { } after)
            parts.Add($"created_at:>='{Iso(after)}'");
        if (filter.CreatedBefore is { } before)
            parts.Add($"created_at:<'{Iso(before)}'");

        if (!string.IsNullOrWhiteSpace(filter.Text))
            parts.Add(filter.Text.Trim());

        return parts.Count == 0 ? null : string.Join(" AND ", parts);
    }

    /// <summary>
    /// Turns user input such as "1042", "#1042" or a full GID into either a GID
    /// or a <c>name:</c> search term.
    /// </summary>
    public static (string? Gid, string? NameQuery) ParseOrderReference(string reference)
    {
        var value = reference.Trim();
        if (value.Length == 0)
            throw new ArgumentException("Order reference is empty.");

        if (value.StartsWith("gid://shopify/Order/", StringComparison.Ordinal))
            return (value, null);

        if (value.All(char.IsAsciiDigit))
            value = "#" + value;

        return (null, $"name:'{value.Replace("'", "\\'")}'");
    }

    private static void AddEnum(List<string> parts, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("any", StringComparison.OrdinalIgnoreCase))
            return;

        var normalized = value.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized))
            throw new ArgumentException(
                $"Invalid {field} '{value}'. Allowed values: any, {string.Join(", ", allowed)}.");

        parts.Add($"{field}:{normalized}");
    }

    private static string Iso(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
