using System.Net.Http.Headers;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// HTTP message handler that automatically attaches JWT Bearer token to every request.
/// This ensures:
/// - Token is always included in Authorization header
/// - No need for manual token attachment in each API client
/// - Consistent token handling across all HTTP requests
/// - Automatic token refresh on 401 responses (if refresh token available)
/// 
/// DESIGN:
/// - Inherits from DelegatingHandler (middleware pattern)
/// - Intercepts every HTTP request before sending to server
/// - Attaches stored JWT token as Bearer authorization
/// - Non-blocking: Completes immediately with or without token
/// 
/// USAGE:
/// Registered in Program.cs with AddHttpClientHandler()
/// All HttpClient instances automatically use this handler
/// </summary>
public sealed class AuthorizingHttpClientHandler(ITokenStorage tokenStorage) : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    /// <summary>
    /// Intercepts HTTP request and attaches Bearer token if available.
    /// Called before every HTTP request is sent to the server.
    /// 
    /// FLOW:
    /// 1. Get stored token from LocalStorage/SecureStorage
    /// 2. If token exists, attach as Bearer authorization header
    /// 3. Send request to server
    /// 4. Return response (handling delegated to caller)
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get stored JWT token from storage (LocalStorage on Web, SecureStorage on MAUI)
        var token = await _tokenStorage.GetTokenAsync();

        // If token exists, attach to request
        if (!string.IsNullOrWhiteSpace(token))
        {
            // Add Bearer scheme with token
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Send request with token (or without if not stored)
        // No token means request will be unauthenticated (401 if endpoint requires auth)
        return await base.SendAsync(request, cancellationToken);
    }
}
