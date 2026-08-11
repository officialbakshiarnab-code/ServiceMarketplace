namespace ServiceMarketplace.Application.Constants;

/// <summary>
/// Administrative permissions are platform-operation privileges.
/// They must not be treated as marketplace capabilities.
/// </summary>
public static class AdministrativePermissionConstants
{
    public const string ClaimType = "administrative_permission";

    public const string PlatformAdmin = "platform.admin";

    public static IReadOnlyList<string> FromRoles(IEnumerable<string> roles)
    {
        var hasAdminRole = roles
            .Select(RoleConstants.NormalizeRole)
            .Any(role => string.Equals(role, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase));

        return hasAdminRole
            ? new[] { PlatformAdmin }
            : Array.Empty<string>();
    }
}
