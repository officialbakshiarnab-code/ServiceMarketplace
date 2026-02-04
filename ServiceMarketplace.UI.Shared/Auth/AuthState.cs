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
    /// Retrieves the primary role claim value from the current user's JWT.
    /// For users with multiple roles, returns the first role.
    /// Role determines dashboard routing (User vs ServiceProvider).
    /// </summary>
    /// <returns>Role claim value, or null if not authenticated or role not found</returns>
    public async Task<string?> GetRoleAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.Role)?.Value;
    }

    /// <summary>
    /// Retrieves all role claims from the current user's JWT.
    /// Supports users with multiple roles (e.g., both User and ServiceProvider).
    /// </summary>
    /// <returns>List of role claim values, or empty list if not authenticated or no roles</returns>
    public async Task<IList<string>> GetAllRolesAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToList();
    }

    /// <summary>
    /// Checks if the current user has a specific role.
    /// Supports multiple role checking.
    /// </summary>
    /// <param name="role">The role to check for</param>
    /// <returns>True if user has the specified role</returns>
    public async Task<bool> HasRoleAsync(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var roles = await GetAllRolesAsync();
        return roles.Any(r => string.Equals(r, role, StringComparison.Ordinal));
    }
}

