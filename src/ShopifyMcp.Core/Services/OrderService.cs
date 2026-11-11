using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Queries;

namespace ShopifyMcp.Core.Services;

public sealed class OrderService(IShopifyGraphQLClient client)
{
    public const int MaxPageSize = 50;

    public async Task<IReadOnlyList<OrderSummary>> ListAsync(OrderFilter filter, int limit, CancellationToken ct = default)
    {
        var variables = new { first = Math.Clamp(limit, 1, MaxPageSize), query = OrderSearchQuery.Build(filter) };
        var data = await client.ExecuteAsync(ShopifyQueries.ListOrders, variables, ct);
        return Wire.Read<Connection<OrderNode>>(data, "orders").Nodes.Select(o => o.ToSummary()).ToList();
    }

    public async Task<OrderDetail> GetAsync(string orderReference, CancellationToken ct = default)
    {
        var id = await ResolveIdAsync(orderReference, ct);
        var data = await client.ExecuteAsync(ShopifyQueries.GetOrder, new { id }, ct);

        if (data.GetProperty("order").ValueKind == System.Text.Json.JsonValueKind.Null)
            throw new ShopifyException($"Order '{orderReference}' was not found.");

        return Wire.Read<OrderNode>(data, "order").ToDetail();
    }

    /// <summary>Sets the order note, or appends to the existing note on a new line.</summary>
    public async Task<OrderNoteResult> UpdateNoteAsync(string orderReference, string note, bool append, CancellationToken ct = default)
    {
        var id = await ResolveIdAsync(orderReference, ct);

        var finalNote = note;
        if (append)
        {
            var existing = (await GetAsync(id, ct)).Note;
            if (!string.IsNullOrWhiteSpace(existing))
                finalNote = existing.TrimEnd() + Environment.NewLine + note;
        }

        var data = await client.ExecuteAsync(
            ShopifyQueries.UpdateOrderNote, new { input = new { id, note = finalNote } }, ct);

        var payload = Wire.Read<OrderUpdatePayload>(data, "orderUpdate");
        if (payload.UserErrors.Count > 0)
            throw new ShopifyException("Shopify rejected the update: " +
                string.Join("; ", payload.UserErrors.Select(e => e.Message)));

        var order = payload.Order ?? throw new ShopifyException("Shopify returned no order after the update.");
        return new OrderNoteResult(order.Id, order.Name, order.Note);
    }

    private async Task<string> ResolveIdAsync(string orderReference, CancellationToken ct)
    {
        var (gid, nameQuery) = OrderSearchQuery.ParseOrderReference(orderReference);
        if (gid is not null)
            return gid;

        var data = await client.ExecuteAsync(ShopifyQueries.FindOrderIdByName, new { query = nameQuery }, ct);
        var match = Wire.Read<Connection<IdNode>>(data, "orders").Nodes.FirstOrDefault();
        return match?.Id ?? throw new ShopifyException($"Order '{orderReference}' was not found.");
    }
}
