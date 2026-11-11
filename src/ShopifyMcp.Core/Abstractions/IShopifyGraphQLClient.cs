using System.Text.Json;

namespace ShopifyMcp.Core.Abstractions;

/// <summary>
/// Sends GraphQL operations to the Shopify Admin API.
/// Implementations handle authentication, throttling and error mapping.
/// </summary>
public interface IShopifyGraphQLClient
{
    /// <summary>Runs an operation and returns the <c>data</c> object of the response.</summary>
    /// <exception cref="ShopifyException">The API returned errors or the request failed.</exception>
    Task<JsonElement> ExecuteAsync(string query, object? variables = null, CancellationToken cancellationToken = default);
}
