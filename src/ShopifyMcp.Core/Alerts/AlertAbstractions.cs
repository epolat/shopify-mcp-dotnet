namespace ShopifyMcp.Core.Alerts;

/// <summary>
/// A notification channel. Add a new channel (email, Slack, WhatsApp...) by implementing
/// this interface and registering it in DI; <see cref="AlertService"/> sends to every registered channel.
/// </summary>
public interface IAlertNotifier
{
    /// <summary>Short channel name shown in results, e.g. "telegram".</summary>
    string Name { get; }

    Task SendAsync(AlertNotification notification, CancellationToken ct = default);
}

/// <summary>Persists alert rules and remembers what was already notified.</summary>
public interface IAlertStore
{
    Task<IReadOnlyList<StockAlertRule>> ListRulesAsync(CancellationToken ct = default);
    Task AddRuleAsync(StockAlertRule rule, CancellationToken ct = default);
    Task<bool> RemoveRuleAsync(string id, CancellationToken ct = default);

    /// <summary>When the given rule last notified about the given item, if ever.</summary>
    Task<DateTimeOffset?> LastNotifiedAsync(string ruleId, string itemKey, CancellationToken ct = default);
    Task MarkNotifiedAsync(string ruleId, IEnumerable<string> itemKeys, DateTimeOffset at, CancellationToken ct = default);
}
