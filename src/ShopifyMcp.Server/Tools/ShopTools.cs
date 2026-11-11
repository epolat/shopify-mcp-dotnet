using System.ComponentModel;
using ModelContextProtocol.Server;
using ShopifyMcp.Core.Models;
using ShopifyMcp.Core.Services;

namespace ShopifyMcp.Server.Tools;

[McpServerToolType]
public sealed class ShopTools(ShopService shop)
{
    [McpServerTool(Name = "get_shop_info", Title = "Get shop info", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Returns the connected store's name, myshopify domain, currency and timezone. " +
                 "Call this first if you need the store's currency or timezone to interpret dates and amounts.")]
    public Task<ShopInfo> GetShopInfo(CancellationToken cancellationToken) =>
        ToolGuard.Run(() => shop.GetInfoAsync(cancellationToken));
}
