using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Components.Authorization;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Custom authentication state provider that reads JWT tokens from storage.
/// Validates token expiration and extracts claims for Blazor authorization.
/// Also detects token expiry and notifies the backend for audit logging.
/// </summary>
public sealed class TokenAuthenticationStateProvider(ITokenStorage tokenStorage, HttpClient httpClient) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly Lock _timerLock = new();
    private Timer? _expiryTimer;

    /// <summary>
    /// Retrieves authentication state by reading and validating stored JWT token.
    /// Returns anonymous state if token is missing, expired, or invalid.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokenStorage.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            ClearExpiryTimer();
            return new AuthenticationState(Anonymous);
        }

        try
        {
            // Parse JWT without validation (signature validation happens server-side)
            var jwt = _tokenHandler.ReadJwtToken(token);
            var expiresAt = jwt.ValidTo;

            // Check if token has expired
            if (expiresAt <= DateTime.UtcNow)
            {
                await tokenStorage.ClearAsync();
                ClearExpiryTimer();
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
                return new AuthenticationState(Anonymous);
            }

            ScheduleExpiry(expiresAt);

            // Create authenticated identity with claims from JWT
            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
            var principal = new ClaimsPrincipal(identity);

            return new AuthenticationState(principal);
        }
        catch
        {
            // If token is malformed or parsing fails, clear it and return anonymous
            await tokenStorage.ClearAsync();
            ClearExpiryTimer();
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
            return new AuthenticationState(Anonymous);
        }
    }

    /// <summary>
    /// Notifies Blazor to re-evaluate authentication state.
    /// Call this after saving a new JWT to storage (e.g., after login).
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Signs out the current user by clearing token storage and notifying Blazor.
    /// Forces immediate UI update to unauthenticated state.
    /// </summary>
    public async Task SignOutAsync()
    {
        await tokenStorage.ClearAsync();
        ClearExpiryTimer();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    private void ScheduleExpiry(DateTime expiresAtUtc)
    {
        var dueTime = expiresAtUtc - DateTime.UtcNow;
        if (dueTime <= TimeSpan.Zero)
        {
            _ = HandleTokenExpiredAsync();
            return;
        }

        lock (_timerLock)
        {
            _expiryTimer?.Dispose();
            _expiryTimer = new Timer(_ => _ = HandleTokenExpiredAsync(), null, dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    private void ClearExpiryTimer()
    {
        lock (_timerLock)
        {
            _expiryTimer?.Dispose();
            _expiryTimer = null;
        }
    }

    private async Task HandleTokenExpiredAsync()
    {
        var token = await tokenStorage.GetTokenAsync();
        
        // Notify backend of session expiry for audit logging
        // This ensures exactly one SessionExpired audit record is created
        await NotifyBackendOfExpiryAsync(token);
        
        await tokenStorage.ClearAsync();
        ClearExpiryTimer();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    /// <summary>
    /// Notifies the backend that a session has expired.
    /// This triggers an audit log entry with EventType = "SessionExpired".
    /// Failures are silently logged to avoid disrupting the logout flow.
    /// </summary>
    private async Task NotifyBackendOfExpiryAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            // Use injected HttpClient which is configured with the API base URL
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            
            // Attempt to post expiry notification to backend
            // If the backend is unreachable, silently fail (logout still succeeds locally)
            using var response = await httpClient.PostAsJsonAsync(
                "api/auth/token-expired",
                new { token = token },
                cts.Token
            );

            // Log result for diagnostics but don't throw
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[TokenAuthenticationStateProvider] Token expiry notification returned status {response.StatusCode}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[TokenAuthenticationStateProvider] Token expiry notification timed out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TokenAuthenticationStateProvider] Failed to notify backend of token expiry: {ex.Message}");
        }
    }
}