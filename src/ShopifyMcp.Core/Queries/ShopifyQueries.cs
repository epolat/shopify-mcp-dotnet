namespace ShopifyMcp.Core.Queries;

/// <summary>
/// Admin GraphQL operations used by the server.
/// Validated against the 2026-07 Admin API schema.
/// </summary>
internal static class ShopifyQueries
{
    public const string ShopInfo = """
        query ShopInfo {
          shop { name myshopifyDomain currencyCode ianaTimezone }
        }
        """;

    public const string ListOrders = """
        query ListOrders($first: Int!, $query: String) {
          orders(first: $first, query: $query, sortKey: CREATED_AT, reverse: true) {
            nodes {
              id name createdAt displayFinancialStatus displayFulfillmentStatus
              totalPriceSet { shopMoney { amount currencyCode } }
              customer { displayName }
            }
          }
        }
        """;

    public const string GetOrder = """
        query GetOrder($id: ID!) {
          order(id: $id) {
            id name createdAt note email displayFinancialStatus displayFulfillmentStatus
            totalPriceSet { shopMoney { amount currencyCode } }
            customer { displayName }
            shippingAddress { name address1 address2 city province country zip }
            lineItems(first: 50) {
              nodes { title quantity sku originalUnitPriceSet { shopMoney { amount currencyCode } } }
            }
          }
        }
        """;

    public const string FindOrderIdByName = """
        query FindOrderIdByName($query: String!) {
          orders(first: 1, query: $query) { nodes { id } }
        }
        """;

    public const string LowStockVariants = """
        query LowStockVariants($first: Int!, $query: String!) {
          productVariants(first: $first, query: $query, sortKey: INVENTORY_QUANTITY) {
            nodes { id title sku inventoryQuantity product { id title } }
          }
        }
        """;

    public const string SearchProducts = """
        query SearchProducts($first: Int!, $query: String) {
          products(first: $first, query: $query) {
            nodes {
              id title handle status totalInventory
              priceRangeV2 { minVariantPrice { amount currencyCode } maxVariantPrice { amount currencyCode } }
            }
          }
        }
        """;

    /// <summary>Runs a ShopifyQL analytics query. Requires the read_reports scope.</summary>
    public const string RunShopifyql = """
        query RunShopifyql($query: String!) {
          shopifyqlQuery(query: $query) {
            tableData { columns { name dataType displayName } rows }
            parseErrors
          }
        }
        """;

    public const string UpdateOrderNote = """
        mutation UpdateOrderNote($input: OrderInput!) {
          orderUpdate(input: $input) {
            order { id name note }
            userErrors { field message }
          }
        }
        """;
}
