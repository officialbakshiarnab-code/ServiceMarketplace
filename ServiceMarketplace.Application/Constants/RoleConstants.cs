namespace ServiceMarketplace.Application.Constants;

/// <summary>
/// Centralized role constants to ensure consistency across the entire application.
/// All role names must use these constants to prevent mismatches.
/// 
/// RULE: Always use these constants instead of hardcoded strings.
/// This ensures that changing a role name requires only updating this file.
/// 
/// ROLE DEFINITIONS:
/// - User: Can create service requests and accept bids
/// - ServiceProvider: Can browse requests and place bids
/// - Both: Has both User and ServiceProvider capabilities (dual-role)
/// - Admin: Has full system access (reserved for future use)
/// </summary>
public static class RoleConstants
{
    /// <summary>
    /// User role - can create service requests and accept bids.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// ServiceProvider role - can browse requests and place bids.
    /// </summary>
    public const string ServiceProvider = "ServiceProvider";

    /// <summary>
    /// Both role - has both User and ServiceProvider capabilities.
    /// Users with this role have multiple role claims in JWT and can access both dashboards.
    /// </summary>
    public const string Both = "Both";

    /// <summary>
    /// Admin role - has full system access (reserved for future use).
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Gets all available role constants in a collection.
    /// Useful for validation and iteration.
    /// </summary>
    public static IReadOnlyList<string> AllRoles { get; } = new[]
    {
        User,
        ServiceProvider,
        Both,
        Admin
    };

    /// <summary>
    /// Validates that a role name is one of the known roles.
    /// </summary>
    /// <param name="role">Role name to validate</param>
    /// <returns>True if role is valid, false otherwise</returns>
    public static bool IsValidRole(string? role)
    {
        return !string.IsNullOrWhiteSpace(role) && AllRoles.Contains(role);
    }

    /// <summary>
    /// Normalizes a role name to the canonical form.
    /// Returns the input if it's already valid, or null if invalid.
    /// </summary>
    /// <param name="role">Role name to normalize</param>
    /// <returns>Normalized role name or null if invalid</returns>
    public static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        // Return exact match if found (case-sensitive)
        if (AllRoles.Contains(role))
            return role;

        // Try case-insensitive match
        var normalized = role.Trim();
        var match = AllRoles.FirstOrDefault(r => r.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return match;
    }
}
