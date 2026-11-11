using System.Text.Json;
using ShopifyMcp.Core.Models;

namespace ShopifyMcp.Core.Queries;

// Shapes that mirror the GraphQL responses 1:1. They are mapped to the flat
// DTOs in Models before leaving the Core layer.

internal static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static T Read<T>(JsonElement data, params string[] path)
    {
        var current = data;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                throw new ShopifyException($"Unexpected response from Shopify: missing '{string.Join('.', path)}'.");
        }
        return current.Deserialize<T>(Json)!;
    }
}

internal sealed record MoneyV2(decimal Amount, string CurrencyCode)
{
    public Money ToModel() => new(Amount, CurrencyCode);
}

internal sealed record MoneyBag(MoneyV2? ShopMoney);

internal sealed record CustomerNode(string? DisplayName);

internal sealed record Connection<T>(List<T> Nodes);

internal sealed record OrderNode(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    string? DisplayFinancialStatus,
    string? DisplayFulfillmentStatus,
    MoneyBag? TotalPriceSet,
    CustomerNode? Customer,
    string? Note = null,
    string? Email = null,
    Address? ShippingAddress = null,
    Connection<LineItemNode>? LineItems = null)
{
    public OrderSummary ToSummary() => new(
        Id, Name, CreatedAt, DisplayFinancialStatus, DisplayFulfillmentStatus,
        TotalPriceSet?.ShopMoney?.ToModel(), Customer?.DisplayName);

    public OrderDetail ToDetail() => new(
        Id, Name, CreatedAt, DisplayFinancialStatus, DisplayFulfillmentStatus,
        TotalPriceSet?.ShopMoney?.ToModel(), Customer?.DisplayName, Email, Note, ShippingAddress,
        LineItems?.Nodes.Select(l => l.ToModel()).ToList() ?? []);
}

internal sealed record LineItemNode(string Title, int Quantity, string? Sku, MoneyBag? OriginalUnitPriceSet)
{
    public OrderLineItem ToModel() => new(Title, Quantity, Sku, OriginalUnitPriceSet?.ShopMoney?.ToModel());
}

internal sealed record IdNode(string Id);

internal sealed record ShopNode(string Name, string MyshopifyDomain, string CurrencyCode, string IanaTimezone);

internal sealed record PriceRange(MoneyV2? MinVariantPrice, MoneyV2? MaxVariantPrice);

internal sealed record ProductNode(
    string Id, string Title, string Handle, string Status, int? TotalInventory, PriceRange? PriceRangeV2)
{
    public ProductSummary ToModel() => new(
        Id, Title, Handle, Status, TotalInventory,
        PriceRangeV2?.MinVariantPrice?.ToModel(), PriceRangeV2?.MaxVariantPrice?.ToModel());
}

internal sealed record ProductRef(string Id, string Title);

internal sealed record VariantNode(string Id, string Title, string? Sku, int? InventoryQuantity, ProductRef Product)
{
    public LowStockVariant ToModel() => new(Id, Product.Id, Product.Title, Title, Sku, InventoryQuantity);
}

internal sealed record ShopifyqlColumn(string Name, string DataType, string? DisplayName);

internal sealed record ShopifyqlTable(List<ShopifyqlColumn> Columns, List<JsonElement> Rows);

internal sealed record ShopifyqlResponse(ShopifyqlTable? TableData, List<string>? ParseErrors);

internal sealed record UserError(List<string>? Field, string Message);

internal sealed record OrderUpdatePayload(OrderNoteNode? Order, List<UserError> UserErrors);

internal sealed record OrderNoteNode(string Id, string Name, string? Note);
