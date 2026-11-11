using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Core.Alerts;
using ShopifyMcp.Core.Services;
using ShopifyMcp.Infrastructure.Alerts;
using ShopifyMcp.Infrastructure.Auth;

namespace ShopifyMcp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShopify(this IServiceCollection services, ShopifyOptions options)
    {
        options.Validate();
        services.AddSingleton(options);
        services.AddHttpClient("shopify", c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<IAccessTokenProvider>(sp => options.UsesClientCredentials
            ? new ClientCredentialsTokenProvider(Http(sp), options)
            : new StaticAccessTokenProvider(options.AccessToken!));

        services.AddSingleton<IShopifyGraphQLClient>(sp => new ShopifyGraphQLClient(
            Http(sp), options,
            sp.GetRequiredService<IAccessTokenProvider>(),
            sp.GetRequiredService<ILogger<ShopifyGraphQLClient>>()));

        services.AddSingleton<ShopService>();
        services.AddSingleton<OrderService>();
        services.AddSingleton<ProductService>();
        services.AddSingleton<AnalyticsService>();
        return services;
    }

    /// <summary>Registers stock alerts and every notification channel that is configured.</summary>
    public static IServiceCollection AddShopifyAlerts(this IServiceCollection services, ShopifyOptions shop, AlertOptions alerts)
    {
        services.AddSingleton(alerts);
        services.AddSingleton<IAlertStore>(new JsonFileAlertStore(
            Path.Combine(alerts.DataDirectory, $"{shop.StoreDomain}.alerts.json")));
        services.AddSingleton(new AlertSettings(shop.StoreDomain, alerts.Cooldown));

        if (alerts.TelegramEnabled)
            services.AddSingleton<IAlertNotifier>(sp =>
                new TelegramNotifier(Http(sp), alerts.TelegramBotToken!, alerts.TelegramChatId!));

        if (alerts.WebhookUrl is { } url)
            services.AddSingleton<IAlertNotifier>(sp => new WebhookNotifier(Http(sp), url));

        // New channels (email, WhatsApp...) go here: implement IAlertNotifier and register it.

        services.AddSingleton(sp => new AlertService(
            sp.GetRequiredService<AnalyticsService>(),
            sp.GetRequiredService<IAlertStore>(),
            sp.GetServices<IAlertNotifier>(),
            sp.GetRequiredService<AlertSettings>()));
        return services;
    }

    private static HttpClient Http(IServiceProvider sp) =>
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("shopify");
}
