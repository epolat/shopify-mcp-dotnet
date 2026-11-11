using System.ComponentModel;
using ModelContextProtocol.Server;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Services;

namespace ShopifyMcp.Server.Tools;

[McpServerToolType]
public sealed class ProductTools(ProductService products)
{
    [McpServerTool(Name = "search_products", Title = "Search products", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Searches products by title or Shopify search syntax (e.g. 'status:active', 'vendor:Acme', 'tag:sale'). " +
                 "Returns title, handle, status, total inventory and price range. Leave query empty to list products.")]
    public Task<IReadOnlyList<ProductSummary>> SearchProducts(
        [Description("Search text or Shopify search syntax.")]
        string? query = null,
        [Description("Maximum number of products to return (1-50).")]
        int limit = 10,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => products.SearchAsync(query, limit, cancellationToken));

    [McpServerTool(Name = "list_low_stock", Title = "List low-stock variants", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists product variants whose inventory quantity is at or below a threshold, lowest stock first. " +
                 "Variants that don't track inventory may show up with quantity 0.")]
    public Task<IReadOnlyList<LowStockVariant>> ListLowStock(
        [Description("Inventory threshold. Variants with this quantity or less are returned.")]
        int threshold = 5,
        [Description("Maximum number of variants to return (1-50).")]
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => products.ListLowStockAsync(threshold, limit, cancellationToken));
}
