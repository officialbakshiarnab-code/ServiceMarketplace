using Microsoft.AspNetCore.Components;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Role-based navigation helper for post-login redirects.
/// Reads role claim from JWT and navigates to appropriate dashboard.
/// </summary>
public sealed class AuthRedirector
{
    private readonly NavigationManager _nav;
    private readonly AuthState _authState;

    public AuthRedirector(NavigationManager nav, AuthState authState)
    {
        _nav = nav;
        _authState = authState;
    }

    /// <summary>
    /// Redirects authenticated user to role-appropriate dashboard.
    /// Role claim values come from API's AuthController:
    /// - "ServiceProvider" ? /provider/dashboard
    /// - "User" (or any other) ? /user/dashboard
    /// </summary>
    public async Task RedirectToDashboardAsync()
    {
        var role = await _authState.GetRoleAsync();

        Console.WriteLine($"[AuthRedirector] Role claim: {role}");

        // WHY: API assigns role claim as "User" or "ServiceProvider" (see AuthController.Login)
        // Must match exactly (case-sensitive)
        if (string.Equals(role, "ServiceProvider", StringComparison.Ordinal))
        {
            Console.WriteLine("[AuthRedirector] Navigating to provider dashboard");
            _nav.NavigateTo("/provider/dashboard", forceLoad: false);
        }
        else
        {
            Console.WriteLine("[AuthRedirector] Navigating to user dashboard");
            _nav.NavigateTo("/user/dashboard", forceLoad: false);
        }
    }
}