using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Centralizes admin route access behavior for shared Web/MAUI UI pages.
/// API policies remain the authoritative security boundary.
/// </summary>
public sealed class AdminAccessGuard
{
    private readonly AuthState _authState;
    private readonly SafeLogoutService _logoutService;
    private readonly NavigationManager _nav;
    private readonly ILogger<AdminAccessGuard> _logger;

    public AdminAccessGuard(
        AuthState authState,
        SafeLogoutService logoutService,
        NavigationManager nav,
        ILogger<AdminAccessGuard> logger)
    {
        _authState = authState;
        _logoutService = logoutService;
        _nav = nav;
        _logger = logger;
    }

    public async Task<bool> EnsureAdminPageAccessAsync()
    {
        if (!await _authState.IsAuthenticatedAsync())
        {
            _nav.NavigateTo("/admin", forceLoad: false);
            return false;
        }

        if (await _authState.HasRoleAsync(RoleNames.Admin))
            return true;

        await SignOutNonAdminAsync("[AdminAccessGuard] Non-admin attempted to access an admin page.");
        return false;
    }

    public async Task<bool> EnsureAdminLoginAccessAsync()
    {
        if (!await _authState.IsAuthenticatedAsync())
            return true;

        if (await _authState.HasRoleAsync(RoleNames.Admin))
        {
            _nav.NavigateTo("/admin/dashboard", forceLoad: false);
            return false;
        }

        await SignOutNonAdminAsync("[AdminAccessGuard] Non-admin attempted to access admin login.");
        return false;
    }

    private async Task SignOutNonAdminAsync(string message)
    {
        _logger.LogWarning(message);
        await _logoutService.LogoutAsync();
        _nav.NavigateTo("/login", forceLoad: true);
    }
}
