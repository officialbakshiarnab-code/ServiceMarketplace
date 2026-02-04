using Microsoft.Extensions.Logging;
using ServiceMarketplace.UI.Shared.Auth;
using System.Net;
using System.Net.Http.Json;

namespace ServiceMarketplace.UI.Web.Services;

/// <summary>
/// HTTP client wrapper that automatically refreshes expired access tokens.
/// Wraps the standard HttpClient to intercept 401 responses.
/// 
/// USAGE:
/// Instead of injecting HttpClient directly, inject TokenRefreshHttpClient.
/// It handles token refresh transparently.
/// 
/// FLOW:
/// 1. Request sent with access token in Authorization header
/// 2. If 401 Unauthorized:
///    a. Extract refresh token from storage
///    b. POST /api/auth/refresh with refresh token
///    c. Store new tokens (rotation)
///    d. Retry original request with new access token
///    e. Return response to caller
/// 3. If refresh also fails (refresh token invalid/expired):
///    a. Clear all tokens
///    b. Force user to re-login
///    c. Return 401 to caller
/// </summary>
public sealed class TokenRefreshHttpClient(
    HttpClient httpClient,
    ITokenStorage tokenStorage,
    TokenAuthenticationStateProvider authStateProvider,
    ILogger<TokenRefreshHttpClient> logger)
{
    private const int MaxRetries = 1;

    /// <summary>
    /// Sends an HTTP request, automatically retrying with a refreshed token if 401 received.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        // Add access token to request
        var token = await tokenStorage.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        // Send request
        var response = await httpClient.SendAsync(request, cancellationToken);

        // If success or not 401, return as-is
        if (response.IsSuccessStatusCode || response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        logger.LogInformation("[TokenRefreshHttpClient] Received 401. Attempting token refresh...");

        // Try to refresh the access token
        var refreshed = await TryRefreshAccessTokenAsync(cancellationToken);

        if (!refreshed)
        {
            logger.LogWarning("[TokenRefreshHttpClient] Token refresh failed. Returning 401.");
            return response;
        }

        logger.LogInformation("[TokenRefreshHttpClient] Token refreshed. Retrying original request...");

        // Clone the request for retry
        using var retryRequest = CloneRequest(request);

        // Attach new access token
        var newToken = await tokenStorage.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(newToken))
        {
            retryRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        }

        // Retry the request
        var retryResponse = await httpClient.SendAsync(retryRequest, cancellationToken);

        logger.LogInformation("[TokenRefreshHttpClient] Retry completed with status {Status}",
            retryResponse.StatusCode);

        return retryResponse;
    }

    /// <summary>
    /// Attempts to refresh the access token using the refresh token.
    /// If successful, stores the new tokens (with rotation).
    /// If unsuccessful, clears all tokens (requires re-login).
    /// </summary>
    private async Task<bool> TryRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var refreshToken = await tokenStorage.GetRefreshTokenAsync();

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                logger.LogWarning("[TokenRefreshHttpClient] No refresh token available");
                await authStateProvider.SignOutAsync();
                return false;
            }

            logger.LogInformation("[TokenRefreshHttpClient] Attempting to refresh with stored refresh token");

            // Create refresh request
            var refreshRequest = new RefreshTokenRequest { RefreshToken = refreshToken };
            using var refreshRequestMessage = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
            {
                Content = JsonContent.Create(refreshRequest)
            };

            // Send refresh request (without Bearer token - endpoint is [AllowAnonymous])
            using var response = await httpClient.SendAsync(refreshRequestMessage, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("[TokenRefreshHttpClient] Refresh endpoint returned {Status}",
                    response.StatusCode);

                // Refresh failed - token is invalid or expired
                // Clear all tokens and require re-login
                await authStateProvider.SignOutAsync();
                return false;
            }

            // Parse response
            var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(
                cancellationToken: cancellationToken);

            if (refreshResponse == null || string.IsNullOrWhiteSpace(refreshResponse.AccessToken))
            {
                logger.LogWarning("[TokenRefreshHttpClient] Refresh response was invalid");
                await authStateProvider.SignOutAsync();
                return false;
            }

            // Store new tokens (with rotation - new refresh token replaces old one)
            await tokenStorage.SaveTokenAsync(refreshResponse.AccessToken);
            await tokenStorage.SaveRefreshTokenAsync(refreshResponse.RefreshToken);

            // Notify auth state provider of new token
            authStateProvider.NotifyAuthenticationStateChanged();

            logger.LogInformation("[TokenRefreshHttpClient] Tokens refreshed successfully");

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TokenRefreshHttpClient] Exception during token refresh");

            // On any error, clear tokens and require re-login
            await authStateProvider.SignOutAsync();
            return false;
        }
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // Copy headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (request.Content is not null)
        {
            // Read content to a byte array
            var contentBytes = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(contentBytes);

            // Copy content headers
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}

/// <summary>
/// Request to refresh an access token.
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Response from token refresh endpoint.
/// </summary>
public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
}
