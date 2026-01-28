using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Provides convenient access to current user's authentication state and claims.
/// Wraps AuthenticationStateProvider for simpler consumption in components.
/// </summary>
public sealed class AuthState
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AuthState(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    /// <returns>True if user has valid JWT token with authenticated identity</returns>
    public async Task<bool> IsAuthenticatedAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true;
    }

    /// <summary>
    /// Retrieves the role claim value from the current user's JWT.
    /// Role determines dashboard routing (User vs ServiceProvider).
    /// </summary>
    /// <returns>Role claim value, or null if not authenticated or role not found</returns>
    public async Task<string?> GetRoleAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.Role)?.Value;
    }
}
