using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ShopifyMcp.Core;

namespace ShopifyMcp.Infrastructure.Auth;

public interface IAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);

    /// <summary>Drops any cached token so the next call fetches a fresh one.</summary>
    void Invalidate();
}

/// <summary>A token copied from a legacy custom app. It never expires, so there is nothing to refresh.</summary>
public sealed class StaticAccessTokenProvider(string token) : IAccessTokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken ct = default) => Task.FromResult(token);
    public void Invalidate() { }
}

/// <summary>
/// Fetches tokens with the OAuth client credentials grant (Dev Dashboard apps installed
/// on a store in the same organization). Tokens live ~24h; this refreshes them shortly before expiry.
/// https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens/client-credentials-grant
/// </summary>
public sealed class ClientCredentialsTokenProvider(
    HttpClient http,
    ShopifyOptions options,
    TimeProvider? timeProvider = null) : IAccessTokenProvider
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (IsValid()) return _token!;

        await _lock.WaitAsync(ct);
        try
        {
            if (IsValid()) return _token!;

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://{options.StoreDomain}/admin/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId!,
                    ["client_secret"] = options.ClientSecret!,
                }),
            };

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new ShopifyException(
                    $"Could not get an access token ({(int)response.StatusCode}). " +
                    "Check SHOPIFY_CLIENT_ID / SHOPIFY_CLIENT_SECRET and that the app is installed on " +
                    $"{options.StoreDomain}, which must belong to the same organization as the app. Shopify said: {Trim(body)}");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new ShopifyException("Shopify returned an empty token response.");

            _token = token.AccessToken;
            _expiresAt = _time.GetUtcNow().AddSeconds(token.ExpiresIn);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _token = null;

    private bool IsValid() => _token is not null && _time.GetUtcNow() < _expiresAt - RefreshMargin;

    private static string Trim(string s) => s.Length <= 300 ? s : s[..300] + "...";

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
