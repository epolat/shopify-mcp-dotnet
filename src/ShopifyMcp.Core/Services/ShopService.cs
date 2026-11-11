using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Queries;

namespace ShopifyMcp.Core.Services;

public sealed class ShopService(IShopifyGraphQLClient client)
{
    public async Task<ShopInfo> GetInfoAsync(CancellationToken ct = default)
    {
        var data = await client.ExecuteAsync(ShopifyQueries.ShopInfo, null, ct);
        var shop = Wire.Read<ShopNode>(data, "shop");
        return new ShopInfo(shop.Name, shop.MyshopifyDomain, shop.CurrencyCode, shop.IanaTimezone);
    }
}
