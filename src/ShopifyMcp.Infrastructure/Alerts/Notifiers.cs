using System.Net.Http.Json;
using ShopifyMcp.Core.Alerts;

namespace ShopifyMcp.Infrastructure.Alerts;

/// <summary>
/// Sends alerts through a Telegram bot.
/// Create a bot with @BotFather, send it a message, then read your chat id from
/// https://api.telegram.org/bot&lt;token&gt;/getUpdates.
/// </summary>
public sealed class TelegramNotifier(HttpClient http, string botToken, string chatId) : IAlertNotifier
{
    public string Name => "telegram";

    public async Task SendAsync(AlertNotification notification, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"https://api.telegram.org/bot{botToken}/sendMessage",
            new { chat_id = chatId, text = notification.ToText(), disable_web_page_preview = true },
            ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Telegram returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }
}

/// <summary>
/// Posts the alert as JSON to any URL. Works out of the box with Slack and Discord incoming
/// webhooks (they read <c>text</c> / <c>content</c>) and with n8n, Zapier or Make for anything else,
/// such as email or WhatsApp.
/// </summary>
public sealed class WebhookNotifier(HttpClient http, Uri url) : IAlertNotifier
{
    public string Name => "webhook";

    public async Task SendAsync(AlertNotification notification, CancellationToken ct = default)
    {
        var text = notification.ToText();
        var payload = new
        {
            text,              // Slack
            content = text.Length > 2000 ? text[..1997] + "..." : text, // Discord (2000 char limit)
            shop = notification.ShopDomain,
            rule = notification.Rule,
            items = notification.Items,
            at = notification.At,
        };

        using var response = await http.PostAsJsonAsync(url, payload, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Webhook returned {(int)response.StatusCode}.");
    }
}
