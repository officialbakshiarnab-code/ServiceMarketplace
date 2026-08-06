using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Validators;

/// <summary>
/// Validates user age requirements based on their role/UserType.
/// 
    /// Rules:
    /// - Customer (UserType.Customer = 1): No age restriction
    /// - Provider (UserType.Provider = 2): Must be 18+ years old
    /// - Admin (UserType.Admin = 3): No age restriction
/// </summary>
public static class AgeValidator
{
    /// <summary>
    /// Minimum age required for service provider role.
    /// </summary>
    private const int MinimumAgeForProvider = 18;

    /// <summary>
    /// Validates user age based on their role.
    /// </summary>
    /// <param name="dateOfBirth">User's date of birth</param>
    /// <param name="role">User role string ("User", "ServiceProvider")</param>
    /// <returns>Validation result with success status and error message</returns>
    public static AgeValidationResult ValidateAge(DateTime dateOfBirth, string role)
    {
        // Normalize role name
        var normalizedRole = RoleConstants.NormalizeRole(role);
        if (normalizedRole == null)
        {
            return AgeValidationResult.Failed($"Invalid role: {role}");
        }

        // Calculate age
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;

        // Account for birthday not yet occurring this year
        if (dateOfBirth.Date > today.AddYears(-age))
        {
            age--;
        }

        // Validate DOB is in the past (not future)
        if (dateOfBirth > DateTime.Today)
        {
            return AgeValidationResult.Failed("Date of birth cannot be in the future");
        }

        // Apply age restrictions based on role
        if (normalizedRole == RoleConstants.ServiceProvider && age < MinimumAgeForProvider)
        {
            return AgeValidationResult.Failed(
                $"Service providers must be at least {MinimumAgeForProvider} years old. " +
                $"You are currently {age} years old.");
        }

        // User role has no age restriction, but validate reasonable age
        if (age > 150)
        {
            return AgeValidationResult.Failed("Invalid date of birth provided");
        }

        // Age validation passed
        return AgeValidationResult.Succeeded();
    }

    /// <summary>
    /// Validates user age based on UserType enum.
    /// </summary>
    /// <param name="dateOfBirth">User's date of birth</param>
    /// <param name="userType">User type enum</param>
    /// <returns>Validation result with success status and error message</returns>
    public static AgeValidationResult ValidateAge(DateTime dateOfBirth, UserType userType)
    {
        // Calculate age
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;

        // Account for birthday not yet occurring this year
        if (dateOfBirth.Date > today.AddYears(-age))
        {
            age--;
        }

        // Validate DOB is in the past (not future)
        if (dateOfBirth > DateTime.Today)
        {
            return AgeValidationResult.Failed("Date of birth cannot be in the future");
        }

        // Apply age restrictions based on UserType
        switch (userType)
        {
            case UserType.Customer:
                // Customer has no age restriction, but validate reasonable age
                if (age > 150)
                {
                    return AgeValidationResult.Failed("Invalid date of birth provided");
                }
                break;
            case UserType.Provider:
                // Providers must be 18+
                if (age < MinimumAgeForProvider)
                {
                    return AgeValidationResult.Failed(
                        $"Service providers must be at least {MinimumAgeForProvider} years old. " +
                        $"You are currently {age} years old.");
                }
                break;
            case UserType.Admin:
                // Admin has no restriction
                break;
            default:
                return AgeValidationResult.Failed($"Unknown user type: {userType}");
        }

        // Age validation passed
        return AgeValidationResult.Succeeded();
    }

    /// <summary>
    /// Gets the minimum required age for a specific role.
    /// </summary>
    /// <param name="role">User role</param>
    /// <returns>Minimum age, or null if no restriction</returns>
    public static int? GetMinimumAgeForRole(string role)
    {
        var normalizedRole = RoleConstants.NormalizeRole(role);
        if (normalizedRole == RoleConstants.ServiceProvider)
        {
            return MinimumAgeForProvider;
        }

        return null; // User role has no minimum age
    }

    /// <summary>
    /// Gets the minimum required age for a specific UserType.
    /// </summary>
    /// <param name="userType">User type enum</param>
    /// <returns>Minimum age, or null if no restriction</returns>
    public static int? GetMinimumAgeForUserType(UserType userType)
    {
        return userType switch
        {
            UserType.Customer => null,
            UserType.Provider => MinimumAgeForProvider,
            UserType.Admin => null,
            _ => null
        };
    }

    /// <summary>
    /// Result of age validation operation.
    /// </summary>
    public sealed class AgeValidationResult
    {
        public bool IsValid { get; private set; }
        public string? Error { get; private set; }

        private AgeValidationResult(bool isValid, string? error)
        {
            IsValid = isValid;
            Error = error;
        }

        public static AgeValidationResult Succeeded() => new(true, null);
        public static AgeValidationResult Failed(string error) => new(false, error);
    }
}
