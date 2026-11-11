using ShopifyMcp.Core.Services;

namespace ShopifyMcp.Core.Alerts;

/// <summary>
/// Evaluates stock alert rules and sends notifications.
/// The MCP server only answers when asked, so something has to call <see cref="CheckAsync"/>
/// on a schedule: run <c>shopify-mcp alerts check</c> from cron, Task Scheduler or GitHub Actions.
/// </summary>
public sealed class AlertService(
    AnalyticsService analytics,
    IAlertStore store,
    IEnumerable<IAlertNotifier> notifiers,
    AlertSettings settings,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public IReadOnlyList<string> Channels => notifiers.Select(n => n.Name).ToList();

    public async Task<StockAlertRule> AddStockRuleAsync(int maxDays, int lookbackDays, string? name, CancellationToken ct = default)
    {
        if (maxDays is < 1 or > 365)
            throw new ArgumentException("maxDays must be between 1 and 365.");
        if (lookbackDays is < 1 or > 365)
            throw new ArgumentException("lookbackDays must be between 1 and 365.");

        var rule = new StockAlertRule(
            Guid.NewGuid().ToString("N")[..8],
            string.IsNullOrWhiteSpace(name) ? $"Stock below {maxDays} days" : name.Trim(),
            maxDays, lookbackDays, _time.GetUtcNow());

        await store.AddRuleAsync(rule, ct);
        return rule;
    }

    public Task<IReadOnlyList<StockAlertRule>> ListRulesAsync(CancellationToken ct = default) => store.ListRulesAsync(ct);

    public Task<bool> RemoveRuleAsync(string id, CancellationToken ct = default) => store.RemoveRuleAsync(id, ct);

    /// <summary>
    /// Evaluates every rule. With <paramref name="notify"/>, sends items that weren't notified
    /// within the cooldown to all channels. A failing channel doesn't stop the others.
    /// </summary>
    public async Task<IReadOnlyList<AlertCheckResult>> CheckAsync(bool notify, CancellationToken ct = default)
    {
        var results = new List<AlertCheckResult>();
        var now = _time.GetUtcNow();

        foreach (var rule in await store.ListRulesAsync(ct))
        {
            var triggered = await analytics.StockRunwayAsync(rule.MaxDays, rule.LookbackDays, 250, ct);

            var fresh = new List<Models.StockRunwayItem>();
            foreach (var item in triggered)
            {
                var last = await store.LastNotifiedAsync(rule.Id, item.Key, ct);
                if (last is null || now - last >= settings.Cooldown)
                    fresh.Add(item);
            }

            var sent = new List<string>();
            var errors = new List<string>();

            if (notify && fresh.Count > 0)
            {
                var message = new AlertNotification(settings.ShopDomain, rule, fresh, now);
                foreach (var notifier in notifiers)
                {
                    try
                    {
                        await notifier.SendAsync(message, ct);
                        sent.Add(notifier.Name);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add($"{notifier.Name}: {ex.Message}");
                    }
                }

                if (sent.Count > 0)
                    await store.MarkNotifiedAsync(rule.Id, fresh.Select(i => i.Key), now, ct);
            }

            results.Add(new AlertCheckResult(rule, triggered, fresh, sent, errors));
        }

        return results;
    }
}

public sealed record AlertSettings(string ShopDomain, TimeSpan Cooldown);
