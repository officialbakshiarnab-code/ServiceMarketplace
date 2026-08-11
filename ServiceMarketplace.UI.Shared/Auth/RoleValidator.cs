using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ServiceMarketplace.Application.Constants;

namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Service for validating and managing user roles with comprehensive logging.
/// Ensures consistent role validation across the UI.
/// 
/// USAGE:
/// var validator = new RoleValidator(authStateProvider, logger);
/// var isValid = await validator.ValidateRoleAsync(ClaimTypes.Role);
/// var hasRole = await validator.HasRoleAsync(RoleConstants.User);
/// </summary>
public sealed class RoleValidator
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<RoleValidator> _logger;

    public RoleValidator(AuthenticationStateProvider authStateProvider, ILogger<RoleValidator> logger)
    {
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Validates that a user has a specific role.
    /// Logs detailed information about validation success/failure.
    /// </summary>
    /// <param name="requiredRole">Role that is required (must match RoleConstants values)</param>
    /// <returns>True if user is authenticated and has the required role</returns>
    public async Task<bool> HasRoleAsync(string requiredRole)
    {
        try
        {
            if (!RoleConstants.IsValidRole(requiredRole))
            {
                _logger.LogWarning("[RoleValidator] Invalid role requested: {Role}. Valid roles are: {ValidRoles}",
                    requiredRole, string.Join(", ", RoleConstants.AllRoles));
                return false;
            }

            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("[RoleValidator] User is not authenticated. Cannot check role.");
                return false;
            }

            var userRoles = user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            if (!userRoles.Any())
            {
                _logger.LogWarning("[RoleValidator] User {UserId} has no role claims. Claims present: {Claims}",
                    user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                    string.Join(", ", user.Claims.Select(c => c.Type)));
                return false;
            }

            var hasRole = userRoles.Contains(requiredRole, StringComparer.Ordinal);

            if (hasRole)
            {
                _logger.LogInformation("[RoleValidator] User has required role: {Role}", requiredRole);
            }
            else
            {
                _logger.LogWarning("[RoleValidator] User role mismatch. Required: {RequiredRole}, Has: {UserRoles}",
                    requiredRole, string.Join(", ", userRoles));
            }

            return hasRole;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoleValidator] Error checking role {Role}: {Error}",
                requiredRole, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the current user's role with validation.
    /// Returns null if role is missing or invalid.
    /// </summary>
    public async Task<string?> GetCurrentRoleAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                _logger.LogInformation("[RoleValidator] User is not authenticated");
                return null;
            }

            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrWhiteSpace(role))
            {
                _logger.LogWarning("[RoleValidator] User {UserId} has no role claim in JWT",
                    user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
                return null;
            }

            if (!RoleConstants.IsValidRole(role))
            {
                _logger.LogWarning("[RoleValidator] User has invalid role: {Role}. Valid roles: {ValidRoles}",
                    role, string.Join(", ", RoleConstants.AllRoles));
                return null;
            }

            _logger.LogInformation("[RoleValidator] Current user role: {Role}", role);
            return role;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoleValidator] Error getting current role: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets all roles the current user has (usually 1, but supports multiple for future extensibility).
    /// </summary>
    public async Task<IReadOnlyList<string>> GetCurrentRolesAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return Array.Empty<string>();
            }

            var roles = user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Where(r => RoleConstants.IsValidRole(r))
                .ToList();

            if (!roles.Any())
            {
                _logger.LogWarning("[RoleValidator] User {UserId} has no valid role claims",
                    user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            }
            else
            {
                _logger.LogInformation("[RoleValidator] User roles: {Roles}", string.Join(", ", roles));
            }

            return roles.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoleValidator] Error getting current roles: {Error}", ex.Message);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Validates the entire JWT role configuration.
    /// Useful for debugging role issues during development.
    /// </summary>
    public async Task<RoleValidationResult> ValidateAsync()
    {
        var result = new RoleValidationResult();

        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            result.IsAuthenticated = user?.Identity?.IsAuthenticated == true;

            if (user is null || !result.IsAuthenticated)
            {
                _logger.LogWarning("[RoleValidator] Validation failed: User is not authenticated");
                return result;
            }

            result.UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            result.Email = user.FindFirst(ClaimTypes.Email)?.Value;
            result.Role = user.FindFirst(ClaimTypes.Role)?.Value;

            // Validate role
            if (string.IsNullOrWhiteSpace(result.Role))
            {
                result.IsValid = false;
                result.Error = "Role claim is missing from JWT";
                _logger.LogWarning("[RoleValidator] Validation failed: {Error}", result.Error);
                return result;
            }

            if (!RoleConstants.IsValidRole(result.Role))
            {
                result.IsValid = false;
                result.Error = $"Role '{result.Role}' is not valid. Valid roles: {string.Join(", ", RoleConstants.AllRoles)}";
                _logger.LogWarning("[RoleValidator] Validation failed: {Error}", result.Error);
                return result;
            }

            result.IsValid = true;
            _logger.LogInformation("[RoleValidator] Validation successful for user {UserId} with role {Role}",
                result.UserId, result.Role);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "[RoleValidator] Validation error: {Error}", ex.Message);
        }

        return result;
    }
}

/// <summary>
/// Result of role validation containing details about the validation.
/// </summary>
public sealed class RoleValidationResult
{
    public bool IsAuthenticated { get; set; }
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Error { get; set; }
}
