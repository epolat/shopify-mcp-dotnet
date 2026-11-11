namespace ShopifyMcp.Core.Models;

// Clean, flat DTOs returned to MCP clients. Kept small on purpose:
// every field ends up in the model's context window.

public sealed record Money(decimal Amount, string CurrencyCode);

public sealed record ShopInfo(string Name, string Domain, string CurrencyCode, string Timezone);

public sealed record OrderSummary(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    string? FinancialStatus,
    string? FulfillmentStatus,
    Money? Total,
    string? Customer);

public sealed record OrderDetail(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    string? FinancialStatus,
    string? FulfillmentStatus,
    Money? Total,
    string? Customer,
    string? Email,
    string? Note,
    Address? ShippingAddress,
    IReadOnlyList<OrderLineItem> LineItems);

public sealed record OrderLineItem(string Title, int Quantity, string? Sku, Money? UnitPrice);

public sealed record Address(
    string? Name,
    string? Address1,
    string? Address2,
    string? City,
    string? Province,
    string? Country,
    string? Zip);

public sealed record OrderNoteResult(string Id, string Name, string? Note);

public sealed record ProductSummary(
    string Id,
    string Title,
    string Handle,
    string Status,
    int? TotalInventory,
    Money? MinPrice,
    Money? MaxPrice);

public sealed record LowStockVariant(
    string VariantId,
    string ProductId,
    string ProductTitle,
    string VariantTitle,
    string? Sku,
    int? InventoryQuantity);
