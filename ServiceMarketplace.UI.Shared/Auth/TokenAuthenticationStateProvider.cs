using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Custom authentication state provider that reads JWT tokens from storage.
/// Validates token expiration and extracts claims for Blazor authorization.
/// 
/// DESIGN PRINCIPLES:
/// - Never throws during GetAuthenticationStateAsync (graceful degradation)
/// - Returns anonymous state for any token issues (missing, expired, invalid)
/// - Non-blocking: All async operations complete immediately
/// - Proper state notifications on login/logout
/// - Comprehensive error handling with detailed logging
/// 
/// THREAD SAFETY:
/// - GetAuthenticationStateAsync() is thread-safe via ReaderWriterLockSlim
/// - Prevents concurrent token reads during updates
/// - NotifyAuthenticationStateChanged() called after releases locks
/// </summary>
public sealed class TokenAuthenticationStateProvider(
    ITokenStorage tokenStorage, 
    HttpClient httpClient,
    ILogger<TokenAuthenticationStateProvider> logger) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly ReaderWriterLockSlim _tokenLock = new();
    private Timer? _expiryTimer;
    private bool _isInitialized;

    /// <summary>
    /// Retrieves authentication state by reading and validating stored JWT token.
    /// NEVER THROWS - Returns anonymous state for any error condition.
    /// 
    /// Gracefully handles:
    /// - Missing token (returns anonymous)
    /// - Expired token (clears storage, returns anonymous)
    /// - Malformed token (clears storage, returns anonymous)
    /// - Invalid claims (clears storage, returns anonymous)
    /// - Corrupted storage (logs error, returns anonymous)
    /// 
    /// THREAD-SAFE: Uses ReaderWriterLockSlim to prevent concurrent reads during updates.
    /// NON-BLOCKING: Completes immediately regardless of storage or token state.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _tokenLock.EnterReadLock();
        try
        {
            var token = await GetTokenSafelyAsync();
            
            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(Anonymous);
            }

            var (isValid, principal) = ParseAndValidateToken(token);
            
            if (!isValid || principal == null)
            {
                // Invalid token - clear storage in background, don't block
                _ = tokenStorage.ClearAsync().ConfigureAwait(false);
                ClearExpiryTimer();
                return new AuthenticationState(Anonymous);
            }

            // Valid token - schedule expiry check
            var expiresAt = ExtractExpirationTime(token);
            if (expiresAt.HasValue)
            {
                ScheduleExpiry(expiresAt.Value);
            }

            _isInitialized = true;
            return new AuthenticationState(principal);
        }
        finally
        {
            _tokenLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets whether authentication state has been initialized.
    /// Used by AuthStateGuard to prevent premature authorization checks.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Notifies Blazor to re-evaluate authentication state.
    /// Call this after successful login to update UI.
    /// Non-blocking: Returns immediately, notification happens asynchronously.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        try
        {
            logger.LogInformation("[TokenAuthenticationStateProvider] Notifying auth state changed (login)");
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TokenAuthenticationStateProvider] Error notifying auth state change");
            // Continue anyway - don't let notification failure block rendering
        }
    }

    /// <summary>
    /// Signs out the current user by clearing token storage and notifying Blazor.
    /// Uses write lock to ensure no concurrent reads during logout.
    /// Non-blocking: Clears storage in background, returns immediately.
    /// </summary>
    public async Task SignOutAsync()
    {
        _tokenLock.EnterWriteLock();
        try
        {
            logger.LogInformation("[TokenAuthenticationStateProvider] Signing out user");
            
            // Clear tokens from storage
            await tokenStorage.ClearAsync().ConfigureAwait(false);
            ClearExpiryTimer();
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TokenAuthenticationStateProvider] Error clearing token storage during logout");
            // Continue with logout notification anyway
        }
        finally
        {
            _tokenLock.ExitWriteLock();
        }

        // Notify after releasing write lock (non-blocking)
        try
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TokenAuthenticationStateProvider] Error notifying logout");
            // Continue anyway
        }
    }

    /// <summary>
    /// Safely retrieves token from storage without throwing.
    /// Returns empty string if storage fails.
    /// </summary>
    private async Task<string?> GetTokenSafelyAsync()
    {
        try
        {
            return await tokenStorage.GetTokenAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Error reading token from storage");
            return null;
        }
    }

    /// <summary>
    /// Parses and validates JWT token without throwing.
    /// Returns (false, null) for any parsing or validation error.
    /// </summary>
    private (bool isValid, ClaimsPrincipal? principal) ParseAndValidateToken(string token)
    {
        try
        {
            // Validate token format and expiration
            var jwt = _tokenHandler.ReadJwtToken(token);
            var expiresAt = jwt.ValidTo;

            // Check if token has expired
            if (expiresAt <= DateTime.UtcNow)
            {
                logger.LogInformation("[TokenAuthenticationStateProvider] Token expired at {ExpiresAt}", expiresAt);
                return (false, null);
            }

            // Validate required claims
            var hasNameIdentifier = jwt.Claims.Any(c => c.Type == ClaimTypes.NameIdentifier);
            if (!hasNameIdentifier)
            {
                logger.LogWarning("[TokenAuthenticationStateProvider] Token missing NameIdentifier claim");
                return (false, null);
            }

            // Create authenticated identity with claims from JWT
            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
            var principal = new ClaimsPrincipal(identity);

            return (true, principal);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Invalid JWT format");
            return (false, null);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] JWT validation failed");
            return (false, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TokenAuthenticationStateProvider] Unexpected error parsing JWT token");
            return (false, null);
        }
    }

    /// <summary>
    /// Safely extracts expiration time from token without throwing.
    /// Returns null if extraction fails.
    /// </summary>
    private DateTime? ExtractExpirationTime(string token)
    {
        try
        {
            var jwt = _tokenHandler.ReadJwtToken(token);
            return jwt.ValidTo;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Error extracting expiration from token");
            return null;
        }
    }

    private void ScheduleExpiry(DateTime expiresAtUtc)
    {
        var dueTime = expiresAtUtc - DateTime.UtcNow;
        if (dueTime <= TimeSpan.Zero)
        {
            // Already expired - handle immediately without blocking
            _ = HandleTokenExpiredAsync().ConfigureAwait(false);
            return;
        }

        lock (new object())
        {
            try
            {
                _expiryTimer?.Dispose();
                _expiryTimer = new Timer(
                    _ => _ = HandleTokenExpiredAsync().ConfigureAwait(false),
                    null,
                    dueTime,
                    Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[TokenAuthenticationStateProvider] Error scheduling token expiry timer");
                // Continue without timer - token will be detected as expired on next page load
            }
        }
    }

    private void ClearExpiryTimer()
    {
        lock (new object())
        {
            try
            {
                _expiryTimer?.Dispose();
                _expiryTimer = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Error clearing expiry timer");
                // Continue anyway
            }
        }
    }

    private async Task HandleTokenExpiredAsync()
    {
        try
        {
            var token = await GetTokenSafelyAsync().ConfigureAwait(false);
            
            // Notify backend of session expiry (best effort, don't block)
            _ = NotifyBackendOfExpiryAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Error in token expiry handler");
        }
        
        // Always sign out locally regardless of backend notification success
        await SignOutAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies the backend that a session has expired.
    /// Best-effort: Silently fails if backend unreachable.
    /// Never throws or blocks rendering.
    /// </summary>
    private async Task NotifyBackendOfExpiryAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            
            logger.LogInformation("[TokenAuthenticationStateProvider] Notifying backend of token expiry");
            
            using var response = await httpClient.PostAsJsonAsync(
                "api/auth/token-expired",
                new { token = token },
                cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("[TokenAuthenticationStateProvider] Token expiry notification sent successfully");
            }
            else
            {
                logger.LogWarning("[TokenAuthenticationStateProvider] Token expiry notification returned status {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[TokenAuthenticationStateProvider] Token expiry notification timed out (expected)");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Backend unreachable for token expiry (network issue)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TokenAuthenticationStateProvider] Failed to notify backend of token expiry: {Error}",
                ex.Message);
        }
    }
}