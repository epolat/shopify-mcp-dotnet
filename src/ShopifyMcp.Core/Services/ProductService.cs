using System.Globalization;
using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Queries;

namespace ShopifyMcp.Core.Services;

public sealed class ProductService(IShopifyGraphQLClient client)
{
    public const int MaxPageSize = 50;

    public async Task<IReadOnlyList<ProductSummary>> SearchAsync(string? query, int limit, CancellationToken ct = default)
    {
        var variables = new
        {
            first = Math.Clamp(limit, 1, MaxPageSize),
            query = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
        };
        var data = await client.ExecuteAsync(ShopifyQueries.SearchProducts, variables, ct);
        return Wire.Read<Connection<ProductNode>>(data, "products").Nodes.Select(p => p.ToModel()).ToList();
    }

    /// <summary>Variants whose inventory is at or below <paramref name="threshold"/>, lowest first.</summary>
    public async Task<IReadOnlyList<LowStockVariant>> ListLowStockAsync(int threshold, int limit, CancellationToken ct = default)
    {
        if (threshold < 0)
            throw new ArgumentException("Threshold can't be negative.");

        var variables = new
        {
            first = Math.Clamp(limit, 1, MaxPageSize),
            query = "inventory_quantity:<=" + threshold.ToString(CultureInfo.InvariantCulture),
        };
        var data = await client.ExecuteAsync(ShopifyQueries.LowStockVariants, variables, ct);
        return Wire.Read<Connection<VariantNode>>(data, "productVariants").Nodes.Select(v => v.ToModel()).ToList();
    }
}
