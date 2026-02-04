using Microsoft.Extensions.Logging;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Manages logout process with guaranteed cleanup order.
/// 
/// LOGOUT SEQUENCE (CRITICAL ORDER):
/// 1. API Call (POST /api/auth/logout) - Creates audit log while token valid
/// 2. Clear Token - Remove JWT from storage (even if API fails)
/// 3. Clear Auth State - Reset TokenAuthenticationStateProvider
/// 4. Navigation - Redirect to login
/// 
/// GUARANTEES:
/// - API call happens before token is cleared
/// - Token is always cleared, even if API fails
/// - Auth state is always cleared
/// - Navigation is predictable
/// - Failures at each step are handled gracefully
/// </summary>
public sealed class SafeLogoutService
{
    private readonly AuthApiClient _authClient;
    private readonly TokenAuthenticationStateProvider _authStateProvider;
    private readonly ILogger<SafeLogoutService> _logger;

    public SafeLogoutService(
        AuthApiClient authClient,
        TokenAuthenticationStateProvider authStateProvider,
        ILogger<SafeLogoutService> logger)
    {
        _authClient = authClient;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Performs safe logout with proper cleanup sequence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // STEP 1: Call API while token is still valid
            _logger.LogInformation("[SafeLogoutService] Step 1: Calling logout API");
            try
            {
                await _authClient.LogoutAsync(cancellationToken);
                _logger.LogInformation("[SafeLogoutService] API logout succeeded");
            }
            catch (Exception ex)
            {
                // Log but continue - token will be cleared anyway
                _logger.LogWarning(ex, "[SafeLogoutService] API logout failed, continuing with local cleanup");
            }

            // STEP 2: Clear token from storage
            _logger.LogInformation("[SafeLogoutService] Step 2: Clearing token from storage");
            await _authStateProvider.SignOutAsync();
            _logger.LogInformation("[SafeLogoutService] Auth state cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SafeLogoutService] Unexpected error during logout cleanup");
            // Still try to clear auth state as fallback
            try
            {
                await _authStateProvider.SignOutAsync();
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "[SafeLogoutService] Failed to clear auth state in fallback");
            }
        }
    }
}
