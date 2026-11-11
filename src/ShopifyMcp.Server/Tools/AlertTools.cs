using System.ComponentModel;
using ModelContextProtocol.Server;
using ShopifyMcp.Core.Alerts;

namespace ShopifyMcp.Server.Tools;

/// <summary>
/// Alert rules live in a local file, not in Shopify, so these tools are available even when
/// SHOPIFY_ALLOW_WRITES is off.
/// </summary>
[McpServerToolType]
public sealed class AlertTools(AlertService alerts)
{
    [McpServerTool(Name = "add_stock_alert", Title = "Add stock alert", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates a rule like 'notify me when any variant has 10 days of stock or less'. " +
                 "Rules are checked by the scheduled `shopify-mcp alerts check` command, which sends notifications " +
                 "to the configured channels (Telegram, webhook). Tell the user if no channel is configured.")]
    public Task<object> AddStockAlert(
        [Description("Notify when a variant has this many days of stock left or fewer (1-365).")]
        int maxDays,
        [Description("Sales pace is measured over this many past days (1-365).")]
        int lookbackDays = 30,
        [Description("Optional friendly name for the rule.")]
        string? name = null,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run<object>(async () => new
        {
            rule = await alerts.AddStockRuleAsync(maxDays, lookbackDays, name, cancellationToken),
            channels = alerts.Channels,
            note = alerts.Channels.Count == 0
                ? "No notification channel is configured. Set TELEGRAM_BOT_TOKEN + TELEGRAM_CHAT_ID or ALERT_WEBHOOK_URL."
                : "Notifications are sent when `shopify-mcp alerts check` runs (schedule it with cron or Task Scheduler).",
        });

    [McpServerTool(Name = "list_alerts", Title = "List alerts", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lists the stock alert rules and the configured notification channels.")]
    public Task<object> ListAlerts(CancellationToken cancellationToken = default) =>
        ToolGuard.Run<object>(async () => new
        {
            rules = await alerts.ListRulesAsync(cancellationToken),
            channels = alerts.Channels,
        });

    [McpServerTool(Name = "remove_alert", Title = "Remove alert", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Deletes a stock alert rule by id.")]
    public Task<object> RemoveAlert(
        [Description("The rule id from list_alerts.")]
        string id,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run<object>(async () => new { removed = await alerts.RemoveRuleAsync(id, cancellationToken) });

    [McpServerTool(Name = "check_alerts", Title = "Check alerts now", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Evaluates every alert rule right now and shows which variants trigger it. " +
                 "With notify=true it also sends notifications for items not notified in the last cooldown window.")]
    public Task<IReadOnlyList<AlertCheckResult>> CheckAlerts(
        [Description("Also send notifications to the configured channels.")]
        bool notify = false,
        CancellationToken cancellationToken = default) =>
        ToolGuard.Run(() => alerts.CheckAsync(notify, cancellationToken));
}
