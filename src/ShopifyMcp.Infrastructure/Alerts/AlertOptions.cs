using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ShopifyMcp.Infrastructure.Alerts;

public sealed class AlertOptions
{
    /// <summary>Where alert rules are stored. Default: ~/.shopify-mcp</summary>
    public required string DataDirectory { get; init; }

    /// <summary>Don't notify about the same variant again within this window. Default: 24 hours.</summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromHours(24);

    public string? TelegramBotToken { get; init; }
    public string? TelegramChatId { get; init; }
    public Uri? WebhookUrl { get; init; }

    public bool TelegramEnabled => TelegramBotToken is not null && TelegramChatId is not null;

    public static AlertOptions FromConfiguration(IConfiguration config)
    {
        var webhook = config["ALERT_WEBHOOK_URL"];
        if (!string.IsNullOrWhiteSpace(webhook) && !Uri.TryCreate(webhook, UriKind.Absolute, out _))
            throw new InvalidOperationException($"ALERT_WEBHOOK_URL is not a valid URL: '{webhook}'.");

        var cooldownHours = double.TryParse(config["ALERT_COOLDOWN_HOURS"], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            ? h : 24;

        return new AlertOptions
        {
            DataDirectory = Blank(config["SHOPIFY_MCP_DATA_DIR"])
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shopify-mcp"),
            Cooldown = TimeSpan.FromHours(Math.Max(0, cooldownHours)),
            TelegramBotToken = Blank(config["TELEGRAM_BOT_TOKEN"]),
            TelegramChatId = Blank(config["TELEGRAM_CHAT_ID"]),
            WebhookUrl = string.IsNullOrWhiteSpace(webhook) ? null : new Uri(webhook),
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
