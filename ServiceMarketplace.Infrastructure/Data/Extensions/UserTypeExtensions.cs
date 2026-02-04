using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Infrastructure.Data.Extensions;

/// <summary>
/// Extension methods for UserType enum to simplify role-based logic.
/// Replace string-based role checks with these methods for better type safety.
/// </summary>
public static class UserTypeExtensions
{
    /// <summary>
    /// Checks if user has User role (can create service requests).
    /// Includes both User and Both types.
    /// </summary>
    public static bool IsUser(this UserType userType)
    {
        return userType == UserType.User || userType == UserType.Both;
    }

    /// <summary>
    /// Checks if user has Provider role (can place bids).
    /// Includes both Provider and Both types.
    /// </summary>
    public static bool IsProvider(this UserType userType)
    {
        return userType == UserType.Provider || userType == UserType.Both;
    }

    /// <summary>
    /// Checks if user has both User and Provider roles.
    /// </summary>
    public static bool IsBothRoles(this UserType userType)
    {
        return userType == UserType.Both;
    }

    /// <summary>
    /// Gets the string representation of the user type for role claims.
    /// Maps enum values to role names used in JWT and authorization.
    /// </summary>
    public static string[] GetRoles(this UserType userType)
    {
        return userType switch
        {
            UserType.User => new[] { RoleConstants.User },
            UserType.Provider => new[] { RoleConstants.ServiceProvider },
            UserType.Both => new[] { RoleConstants.User, RoleConstants.ServiceProvider },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Gets the primary role for display and routing.
    /// For Both type, returns User role (as primary role).
    /// </summary>
    public static string GetPrimaryRole(this UserType userType)
    {
        return userType switch
        {
            UserType.User => RoleConstants.User,
            UserType.Provider => RoleConstants.ServiceProvider,
            UserType.Both => RoleConstants.User, // Primary role for dashboard routing
            _ => RoleConstants.User
        };
    }

    /// <summary>
    /// Converts role string to UserType enum.
    /// Used when reading from database or JWT claims.
    /// </summary>
    public static UserType? FromRole(string role)
    {
        if (string.Equals(role, RoleConstants.User, StringComparison.Ordinal))
            return UserType.User;
        if (string.Equals(role, RoleConstants.ServiceProvider, StringComparison.Ordinal))
            return UserType.Provider;
        return null;
    }

    /// <summary>
    /// Converts multiple role strings to UserType enum.
    /// Returns Both if both User and Provider roles are present.
    /// </summary>
    public static UserType FromRoles(IEnumerable<string> roles)
    {
        var roleList = roles.ToList();
        var hasUser = roleList.Any(r => string.Equals(r, RoleConstants.User, StringComparison.Ordinal));
        var hasProvider = roleList.Any(r => string.Equals(r, RoleConstants.ServiceProvider, StringComparison.Ordinal));

        if (hasUser && hasProvider)
            return UserType.Both;
        if (hasProvider)
            return UserType.Provider;
        return UserType.User; // Default to User
    }

    /// <summary>
    /// Validates if the given value is a valid UserType.
    /// </summary>
    public static bool IsValid(int value)
    {
        return value >= 1 && value <= 3;
    }
}
