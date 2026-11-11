using System.ComponentModel;
using ModelContextProtocol.Server;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Services;

namespace ShopifyMcp.Server.Tools;

[McpServerToolType]
public sealed class OrderTools(OrderService orders)
{
    [McpServerTool(Name = "list_orders", Title = "List orders", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists orders, newest first. All filters are optional and combined with AND. " +
                 "Returns id, order name (e.g. #1042), date, payment and fulfillment status, total and customer name.")]
    public Task<IReadOnlyList<OrderSummary>> ListOrders(
        [Description("Order status: any, open, closed, cancelled, not_closed.")]
        string? status = null,
        [Description("Fulfillment status: any, unfulfilled, unshipped, partial, fulfilled, shipped, scheduled, on_hold, request_declined.")]
        string? fulfillmentStatus = null,
        [Description("Payment status: any, paid, pending, authorized, partially_paid, partially_refunded, refunded, voided, expired.")]
        string? financialStatus = null,
        [Description("Only orders created at or after this time. ISO 8601, e.g. 2026-11-01 or 2026-11-01T09:00:00Z.")]
        DateTimeOffset? createdAfter = null,
        [Description("Only orders created before this time. ISO 8601.")]
        DateTimeOffset? createdBefore = null,
        [Description("Free-text search or extra Shopify search syntax, e.g. a customer email, 'tag:wholesale' or 'sku:ABC-1'.")]
        string? search = null,
        [Description("Maximum number of orders to return (1-50).")]
        int limit = 10,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => orders.ListAsync(
            new OrderFilter(status, fulfillmentStatus, financialStatus, createdAfter, createdBefore, search),
            limit, cancellationToken));

    [McpServerTool(Name = "get_order", Title = "Get order details", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Returns one order with its line items, shipping address, note and email.")]
    public Task<OrderDetail> GetOrder(
        [Description("Order number (1042 or #1042) or the order GID (gid://shopify/Order/...).")]
        string order,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => orders.GetAsync(order, cancellationToken));
}

/// <summary>Registered only when SHOPIFY_ALLOW_WRITES=true.</summary>
[McpServerToolType]
public sealed class OrderWriteTools(OrderService orders)
{
    [McpServerTool(Name = "update_order_note", Title = "Update order note", Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Adds a note to an order. By default the text is appended on a new line below the existing note; " +
                 "set append=false to replace the whole note.")]
    public Task<OrderNoteResult> UpdateOrderNote(
        [Description("Order number (1042 or #1042) or the order GID.")]
        string order,
        [Description("The note text.")]
        string note,
        [Description("true: append to the existing note (default). false: replace it.")]
        bool append = true,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => orders.UpdateNoteAsync(order, note, append, cancellationToken));
}
