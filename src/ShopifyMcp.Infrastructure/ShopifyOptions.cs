using Microsoft.Extensions.Configuration;

namespace ShopifyMcp.Infrastructure;

/// <summary>
/// Settings read from environment variables (or any other configuration source).
/// Supply either <see cref="AccessToken"/> or <see cref="ClientId"/> + <see cref="ClientSecret"/>.
/// </summary>
public sealed class ShopifyOptions
{
    public const string DefaultApiVersion = "2026-07";

    /// <summary>The store's myshopify.com domain, e.g. <c>my-store.myshopify.com</c>.</summary>
    public required string StoreDomain { get; init; }

    /// <summary>A static Admin API access token (legacy custom apps, <c>shpat_...</c>).</summary>
    public string? AccessToken { get; init; }

    /// <summary>Client ID of a Dev Dashboard app, used with the client credentials grant.</summary>
    public string? ClientId { get; init; }

    /// <summary>Client secret of a Dev Dashboard app, used with the client credentials grant.</summary>
    public string? ClientSecret { get; init; }

    public string ApiVersion { get; init; } = DefaultApiVersion;

    /// <summary>When false (the default), tools that change store data are not registered.</summary>
    public bool AllowWrites { get; init; }

    public bool UsesClientCredentials => string.IsNullOrWhiteSpace(AccessToken);

    public static ShopifyOptions FromConfiguration(IConfiguration config)
    {
        var options = new ShopifyOptions
        {
            StoreDomain = NormalizeDomain(config["SHOPIFY_STORE_DOMAIN"]),
            AccessToken = Blank(config["SHOPIFY_ACCESS_TOKEN"]),
            ClientId = Blank(config["SHOPIFY_CLIENT_ID"]),
            ClientSecret = Blank(config["SHOPIFY_CLIENT_SECRET"]),
            ApiVersion = Blank(config["SHOPIFY_API_VERSION"]) ?? DefaultApiVersion,
            AllowWrites = bool.TryParse(config["SHOPIFY_ALLOW_WRITES"], out var allow) && allow,
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StoreDomain))
            throw new InvalidOperationException(
                "SHOPIFY_STORE_DOMAIN is not set. Example: SHOPIFY_STORE_DOMAIN=my-store.myshopify.com");

        if (UsesClientCredentials && (ClientId is null || ClientSecret is null))
            throw new InvalidOperationException(
                "No credentials found. Set SHOPIFY_CLIENT_ID and SHOPIFY_CLIENT_SECRET (Dev Dashboard app), " +
                "or SHOPIFY_ACCESS_TOKEN (legacy custom app).");
    }

    /// <summary>Accepts "my-store", "my-store.myshopify.com" or "https://my-store.myshopify.com/".</summary>
    public static string NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var domain = value.Trim().ToLowerInvariant();
        if (domain.StartsWith("https://")) domain = domain["https://".Length..];
        if (domain.StartsWith("http://")) domain = domain["http://".Length..];
        domain = domain.TrimEnd('/');

        return domain.Contains('.') ? domain : domain + ".myshopify.com";
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
