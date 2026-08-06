using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Infrastructure.Data.Extensions;

/// <summary>
/// Extension methods for UserType enum adapted to new enum values (Customer, Provider, Admin).
/// </summary>
public static class UserTypeExtensions
{
    public static bool IsCustomer(this UserType userType) => userType == UserType.Customer;
    public static bool IsProvider(this UserType userType) => userType == UserType.Provider;
    public static bool IsAdmin(this UserType userType) => userType == UserType.Admin;

    public static string GetPrimaryRole(this UserType userType)
    {
        return userType switch
        {
            UserType.Customer => RoleConstants.User,
            UserType.Provider => RoleConstants.ServiceProvider,
            UserType.Admin => RoleConstants.Admin,
            _ => RoleConstants.User
        };
    }

    public static UserType? FromRole(string role)
    {
        if (string.Equals(role, RoleConstants.User, StringComparison.OrdinalIgnoreCase))
            return UserType.Customer;
        if (string.Equals(role, RoleConstants.ServiceProvider, StringComparison.OrdinalIgnoreCase))
            return UserType.Provider;
        if (string.Equals(role, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
            return UserType.Admin;
        return null;
    }

    public static bool IsValid(int value) => value >= 1 && value <= 3;
}
