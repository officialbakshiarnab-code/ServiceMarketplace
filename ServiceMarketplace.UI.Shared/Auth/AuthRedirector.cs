using Microsoft.AspNetCore.Components;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Role-based navigation helper for post-login redirects.
/// Reads role claims from JWT and navigates to appropriate dashboard.
/// 
/// Navigation Logic:
/// - If user has BOTH User and ServiceProvider roles ? /provider/dashboard (default to Provider)
/// - If user has ONLY ServiceProvider role ? /provider/dashboard
/// - If user has ONLY User role ? /user/dashboard
/// - If user has no valid roles ? /user/dashboard (safe default)
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
            // Retrieve all roles assigned to the user
            var roles = await _authState.GetAllRolesAsync();

            Console.WriteLine($"[AuthRedirector] User roles: {string.Join(", ", roles)}");

            // Determine which dashboard to navigate to based on roles
            var navigateTarget = DetermineDashboard(roles);

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
    /// Determines which dashboard to navigate to based on user roles.
    /// 
    /// Priority (to support UserType.Both - dual-role users):
    /// 1. If user has Both role ? /provider/dashboard (dual-role users default to Provider)
    /// 2. If user has ServiceProvider role (without Both) ? /provider/dashboard
    /// 3. If user has only User role ? /user/dashboard
    /// 4. If user has no roles or invalid roles ? /user/dashboard (safe default)
    /// 
    /// Rationale:
    /// Users with UserType.Both have BOTH "User" and "ServiceProvider" role claims in JWT.
    /// We default to Provider dashboard because:
    /// - Provider features are more complex and require explicit access
    /// - Users can always navigate to User dashboard if needed
    /// - Provider dashboard is the "power user" experience
    /// </summary>
    /// <param name="roles">List of all roles assigned to the user</param>
    /// <returns>The dashboard path to navigate to</returns>
    private static string DetermineDashboard(IList<string> roles)
    {
        // Normalize and validate roles
        var validRoles = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList();

        Console.WriteLine($"[AuthRedirector] Valid roles after normalization: {string.Join(", ", validRoles)}");

        // Check if user has BOTH role (dual-role users)
        if (validRoles.Any(r => string.Equals(r, "Both", StringComparison.Ordinal)))
        {
            Console.WriteLine("[AuthRedirector] User has Both role, navigating to provider dashboard (default for dual-role)");
            return "/provider/dashboard";
        }

        // Check if user has ServiceProvider role
        // If yes, navigate to provider dashboard (whether or not they also have User role)
        if (validRoles.Any(r => string.Equals(r, RoleNames.Provider, StringComparison.Ordinal)))
        {
            Console.WriteLine("[AuthRedirector] User has ServiceProvider role, navigating to provider dashboard");
            return "/provider/dashboard";
        }

        // Check if user has User role (and no ServiceProvider or Both role)
        if (validRoles.Any(r => string.Equals(r, RoleNames.User, StringComparison.Ordinal)))
        {
            Console.WriteLine("[AuthRedirector] User has User role only, navigating to user dashboard");
            return "/user/dashboard";
        }

        // No valid roles or empty role list - safe default
        Console.WriteLine("[AuthRedirector] User has no valid roles, using safe default: user dashboard");
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