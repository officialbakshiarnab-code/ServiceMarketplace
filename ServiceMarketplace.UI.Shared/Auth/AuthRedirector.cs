using Microsoft.AspNetCore.Components;
using ServiceMarketplace.Application.Constants;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Role-based navigation helper for post-login redirects.
/// Reads capability claims from JWT and navigates to the appropriate dashboard.
/// 
/// Navigation Logic:
/// - If user has service.provider capability, default to provider dashboard.
/// - If user has service.customer capability, default to user dashboard.
/// - If user has no recognized capability, default to user dashboard.
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
    /// 
    /// This method should be called ONLY AFTER:
    /// 1. AuthStateProvider.NotifyAuthenticationStateChanged() has been called
    /// 2. JWT token has been stored and is accessible
    /// 3. Auth state has been updated by Blazor
    /// </summary>
    public async Task RedirectToDashboardAsync()
    {
        try
        {
            var capabilities = await _authState.GetAllMarketplaceCapabilitiesAsync();

            Console.WriteLine($"[AuthRedirector] User capabilities: {string.Join(", ", capabilities)}");

            var navigateTarget = DetermineDashboard(capabilities);

            Console.WriteLine($"[AuthRedirector] Redirecting to: {navigateTarget}");

            _nav.NavigateTo(navigateTarget, forceLoad: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthRedirector] Error during role-based redirection: {ex.Message}");
            // Fallback: Navigate to user dashboard as safe default
            _nav.NavigateTo("/user/dashboard", forceLoad: false);
        }
    }

    /// <summary>
    /// Determines which dashboard to navigate to based on marketplace capabilities.
    /// </summary>
    /// <param name="capabilities">List of all marketplace capabilities assigned to the user</param>
    /// <returns>The dashboard path to navigate to</returns>
    private static string DetermineDashboard(IList<string> capabilities)
    {
        var validCapabilities = capabilities
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList();

        Console.WriteLine($"[AuthRedirector] Valid capabilities after normalization: {string.Join(", ", validCapabilities)}");

        if (validCapabilities.Any(c => string.Equals(c, MarketplaceCapabilityConstants.ServiceProvider, StringComparison.Ordinal)))
        {
            Console.WriteLine("[AuthRedirector] User has service.provider capability, navigating to provider dashboard");
            return "/provider/dashboard";
        }

        if (validCapabilities.Any(c => string.Equals(c, MarketplaceCapabilityConstants.ServiceCustomer, StringComparison.Ordinal)))
        {
            Console.WriteLine("[AuthRedirector] User has service.customer capability, navigating to user dashboard");
            return "/user/dashboard";
        }

        Console.WriteLine("[AuthRedirector] User has no recognized marketplace capability, using safe default: user dashboard");
        return "/user/dashboard";
    }

    /// <summary>
    /// Alternative method that can be used if you want to manually specify which dashboard to use.
    /// Useful for scenarios where redirect logic needs to be customized per feature.
    /// </summary>
    /// <param name="dashboard">The dashboard path to navigate to (e.g., "/provider/dashboard", "/user/dashboard")</param>
    public void NavigateToDashboard(string dashboard)
    {
        if (string.IsNullOrWhiteSpace(dashboard))
        {
            Console.WriteLine("[AuthRedirector] Invalid dashboard path provided");
            dashboard = "/user/dashboard";
        }

        Console.WriteLine($"[AuthRedirector] Manually navigating to: {dashboard}");
        _nav.NavigateTo(dashboard, forceLoad: false);
    }
}
