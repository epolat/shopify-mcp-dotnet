using System.Text;
using ShopifyMcp.Core.Models;

namespace ShopifyMcp.Core.Alerts;

/// <summary>
/// "Tell me when a variant has less than <see cref="MaxDays"/> days of stock left,
/// based on the sales pace of the last <see cref="LookbackDays"/> days."
/// </summary>
public sealed record StockAlertRule(
    string Id,
    string Name,
    int MaxDays,
    int LookbackDays,
    DateTimeOffset CreatedAt);

/// <summary>One rule that fired, with the variants that triggered it.</summary>
public sealed record AlertNotification(
    string ShopDomain,
    StockAlertRule Rule,
    IReadOnlyList<StockRunwayItem> Items,
    DateTimeOffset At)
{
    /// <summary>Plain-text message, ready for Telegram, email, Slack and similar channels.</summary>
    public string ToText()
    {
        var sb = new StringBuilder()
            .AppendLine($"⚠️ {ShopDomain}: {Items.Count} variant(s) have ≤ {Rule.MaxDays} days of stock left")
            .AppendLine($"Rule: {Rule.Name} (pace of the last {Rule.LookbackDays} days)")
            .AppendLine();

        foreach (var item in Items.Take(25))
        {
            var variant = string.IsNullOrWhiteSpace(item.VariantTitle) || item.VariantTitle == "Default Title"
                ? "" : $" / {item.VariantTitle}";
            var sku = string.IsNullOrWhiteSpace(item.Sku) ? "" : $" [{item.Sku}]";
            sb.AppendLine($"• {item.ProductTitle}{variant}{sku}: {Fmt(item.DaysRemaining)} days left " +
                          $"({Fmt(item.UnitsLeft)} units, {Fmt(item.UnitsSoldPerDay)}/day)");
        }

        if (Items.Count > 25)
            sb.AppendLine($"…and {Items.Count - 25} more.");

        return sb.ToString().TrimEnd();
    }

    private static string Fmt(decimal? value) =>
        value is { } v ? Math.Round(v, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
}

public sealed record AlertCheckResult(
    StockAlertRule Rule,
    IReadOnlyList<StockRunwayItem> Triggered,
    IReadOnlyList<StockRunwayItem> NewSinceLastNotification,
    IReadOnlyList<string> NotifiedChannels,
    IReadOnlyList<string> Errors);
