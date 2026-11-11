namespace ShopifyMcp.Core;

/// <summary>
/// An error whose message is safe and useful to show to the end user
/// (for example "Order #1042 was not found" or "Access denied: missing read_orders scope").
/// </summary>
public class ShopifyException(string message, Exception? inner = null) : Exception(message, inner);
