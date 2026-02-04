using ServiceMarketplace.Application.Constants;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// UI role constants that must match Application.Constants.RoleConstants.
/// These are duplicated for UI convenience to avoid cross-project references in Razor components.
/// 
/// IMPORTANT: Keep these in sync with RoleConstants in ServiceMarketplace.Application.
/// Any role name changes must be made in both places.
/// </summary>
public static class RoleNames
{
    /// <summary>
    /// User role - can create service requests and accept bids
    /// </summary>
    public const string User = RoleConstants.User;

    /// <summary>
    /// ServiceProvider role - can browse requests and place bids
    /// </summary>
    public const string Provider = RoleConstants.ServiceProvider;

    /// <summary>
    /// Admin role - has full system access including audit logs
    /// </summary>
    public const string Admin = RoleConstants.Admin;

    /// <summary>
    /// Gets all available roles
    /// </summary>
    public static IReadOnlyList<string> AllRoles => RoleConstants.AllRoles;

    /// <summary>
    /// Validates if a role is one of the known roles
    /// </summary>
    public static bool IsValidRole(string? role) => RoleConstants.IsValidRole(role);

    /// <summary>
    /// Normalizes a role name to the canonical form
    /// </summary>
    public static string? NormalizeRole(string? role) => RoleConstants.NormalizeRole(role);
}
