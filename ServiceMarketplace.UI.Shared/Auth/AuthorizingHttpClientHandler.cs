using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>Attach the current token and replay once after an explicit expiry challenge.</summary>
public sealed class AuthorizingHttpClientHandler(
    ITokenStorage tokenStorage, SessionTokenRefreshService session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (!uri.IsAbsoluteUri || uri.Scheme != session.ApiBaseUri.Scheme || uri.Authority != session.ApiBaseUri.Authority)
        {
            request.Headers.Authorization = null;
            return await base.SendAsync(request, cancellationToken);
        }
        var path = uri.AbsolutePath.TrimEnd('/');
        var anonymousAuth = path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/auth/register", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/auth/token-expired", StringComparison.OrdinalIgnoreCase);
        var generation = session.Generation;
        var token = anonymousAuth ? null : await tokenStorage.GetTokenAsync();
        // Overwrite legacy default headers, including clearing stale tokens after logout.
        request.Headers.Authorization = string.IsNullOrWhiteSpace(token)
            ? null : new AuthenticationHeaderValue("Bearer", token);
        using var replay = token == null ? null : await CloneAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (token == null || !await IsExpiryChallengeAsync(response, cancellationToken)) return response;
        if (!await session.RefreshAsync(token, cancellationToken, generation)) return response;
        var refreshed = await tokenStorage.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshed) || session.Generation != generation) return response;
        response.Dispose();
        replay!.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        // Direct inner send: no recursive handler call or second authentication retry.
        var retried = await base.SendAsync(replay, cancellationToken);
        if (retried.StatusCode == HttpStatusCode.Unauthorized) await session.SignOutAsync(generation);
        return retried;
    }

    private static async Task<bool> IsExpiryChallengeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized) return false;
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return body.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String && error.GetString() == "access_token_expired";
        }
        catch (JsonException) { return false; }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version, VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options) clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        if (request.Content != null)
        {
            clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}
