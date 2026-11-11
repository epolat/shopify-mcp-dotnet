using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShopifyMcp.Core;
using ShopifyMcp.Core.Abstractions;
using ShopifyMcp.Infrastructure.Auth;

namespace ShopifyMcp.Infrastructure;

/// <summary>
/// Admin GraphQL client. Retries throttled requests with backoff and refreshes
/// the token once if Shopify rejects it.
/// </summary>
public sealed class ShopifyGraphQLClient(
    HttpClient http,
    ShopifyOptions options,
    IAccessTokenProvider tokens,
    ILogger<ShopifyGraphQLClient> logger,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : IShopifyGraphQLClient
{
    internal const int MaxAttempts = 4;

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly Uri _endpoint = new($"https://{options.StoreDomain}/admin/api/{options.ApiVersion}/graphql.json");

    public async Task<JsonElement> ExecuteAsync(string query, object? variables = null, CancellationToken cancellationToken = default)
    {
        var refreshedToken = false;

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(new { query, variables }),
            };
            request.Headers.Add("X-Shopify-Access-Token", await tokens.GetTokenAsync(cancellationToken));

            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedToken)
            {
                // A cached client-credentials token may have been revoked; try once with a fresh one.
                tokens.Invalidate();
                refreshedToken = true;
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
            {
                await BackoffAsync(attempt, cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new ShopifyException(DescribeHttpError(response.StatusCode));

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                if (IsThrottled(errors) && attempt < MaxAttempts)
                {
                    await BackoffAsync(attempt, cancellationToken);
                    continue;
                }
                throw new ShopifyException("Shopify returned errors: " + string.Join("; ", Messages(errors)));
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new ShopifyException("Shopify returned a response without data.");

            return data.Clone();
        }
    }

    private Task BackoffAsync(int attempt, CancellationToken ct)
    {
        var wait = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s
        logger.LogWarning("Shopify throttled the request, retrying in {Seconds}s (attempt {Attempt})", wait.TotalSeconds, attempt);
        return _delay(wait, ct);
    }

    private static bool IsThrottled(JsonElement errors) =>
        errors.EnumerateArray().Any(e =>
            e.TryGetProperty("extensions", out var ext) &&
            ext.TryGetProperty("code", out var code) &&
            code.GetString() == "THROTTLED");

    private static IEnumerable<string> Messages(JsonElement errors) =>
        errors.EnumerateArray().Select(e =>
            e.TryGetProperty("message", out var m) ? m.GetString() ?? "unknown error" : e.ToString());

    private string DescribeHttpError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Shopify rejected the access token (401). Check your credentials.",
        HttpStatusCode.Forbidden => "Access denied (403). The app is probably missing an access scope such as read_orders or read_products.",
        HttpStatusCode.NotFound => $"Not found (404). Check SHOPIFY_STORE_DOMAIN ('{options.StoreDomain}') and SHOPIFY_API_VERSION ('{options.ApiVersion}').",
        HttpStatusCode.PaymentRequired => "The store is frozen or its plan is unpaid (402).",
        HttpStatusCode.Locked => "The store is locked (423).",
        HttpStatusCode.TooManyRequests => "Shopify kept throttling the request (429). Try again in a moment.",
        _ => $"Shopify request failed with HTTP {(int)status}.",
    };
}
