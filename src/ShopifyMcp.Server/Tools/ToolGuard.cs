using ModelContextProtocol;
using ShopifyMcp.Core;

namespace ShopifyMcp.Server.Tools;

/// <summary>
/// The MCP SDK hides the message of unexpected exceptions from the client.
/// Errors we expect (bad input, Shopify errors) are rethrown as <see cref="McpException"/>
/// so the assistant sees what went wrong and can correct itself.
/// </summary>
internal static class ToolGuard
{
    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ShopifyException or ArgumentException)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
