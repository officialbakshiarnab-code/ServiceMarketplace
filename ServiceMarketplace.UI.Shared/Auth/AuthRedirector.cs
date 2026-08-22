using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Constants;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Role-based navigation helper for post-login redirects.
/// Reads capability claims from JWT and navigates to the appropriate dashboard.
/// 
/// Navigation Logic:
/// - If user has Admin role, default to admin dashboard.
/// - If user has service.provider capability, default to provider dashboard.
/// - If user has service.customer capability, default to user dashboard.
/// - If user has no recognized capability, default to user dashboard.
/// </summary>
public sealed class AuthRedirector
{
    private readonly NavigationManager _nav;
    private readonly AuthState _authState;
    private readonly ILogger<AuthRedirector> _logger;

    public AuthRedirector(NavigationManager nav, AuthState authState, ILogger<AuthRedirector> logger)
    {
        _nav = nav;
        _authState = authState;
        _logger = logger;
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
            if (await _authState.HasRoleAsync(RoleNames.Admin))
            {
                _logger.LogInformation("[AuthRedirector] Admin role detected, redirecting to admin dashboard");
                _nav.NavigateTo("/admin/dashboard", forceLoad: false);
                return;
            }

            var capabilities = await _authState.GetAllMarketplaceCapabilitiesAsync();

            _logger.LogInformation("[AuthRedirector] User capabilities: {Capabilities}", string.Join(", ", capabilities));

            var navigateTarget = DetermineDashboard(capabilities);

            _logger.LogInformation("[AuthRedirector] Redirecting to: {NavigateTarget}", navigateTarget);

            _nav.NavigateTo(navigateTarget, forceLoad: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthRedirector] Error during role-based redirection");
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

        if (validCapabilities.Any(c => string.Equals(c, MarketplaceCapabilityConstants.ServiceProvider, StringComparison.Ordinal)))
            return "/provider/dashboard";

        if (validCapabilities.Any(c => string.Equals(c, MarketplaceCapabilityConstants.ServiceCustomer, StringComparison.Ordinal)))
            return "/user/dashboard";

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
            _logger.LogWarning("[AuthRedirector] Invalid dashboard path provided");
            dashboard = "/user/dashboard";
        }

        _logger.LogInformation("[AuthRedirector] Manually navigating to: {Dashboard}", dashboard);
        _nav.NavigateTo(dashboard, forceLoad: false);
    }
}
