using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Constants;

namespace ServiceMarketplace.API.Services;

/// <summary>
/// Service responsible for seeding application roles on startup.
/// Ensures that all required roles (User, ServiceProvider, Admin) are created exactly once.
/// 
/// CRITICAL BEHAVIOR:
/// - Runs once during application startup
/// - Never runs during user registration
/// - Creates each role only if it doesn't exist
/// - Logs all role creation attempts
/// - Thread-safe via database constraints
/// 
/// WHY: Separating role creation from user registration prevents
/// duplicate role creation attempts and ensures roles are available
/// before any users are registered.
/// </summary>
public sealed class RoleSeedingService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<RoleSeedingService> _logger;

    public RoleSeedingService(RoleManager<IdentityRole> roleManager, ILogger<RoleSeedingService> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// Seeds all required application roles.
    /// Call this method once during application startup, after database migrations.
    /// 
    /// This method:
    /// 1. Iterates through all roles in RoleConstants.AllRoles
    /// 2. Checks if each role exists in the database
    /// 3. Creates any missing roles
    /// 4. Logs all operations
    /// 5. Never throws exceptions - catches and logs errors gracefully
    /// </summary>
    public async Task SeedRolesAsync()
    {
        _logger.LogInformation("[RoleSeedingService] Starting role seeding process");

        try
        {
            foreach (var roleName in RoleConstants.AllRoles)
            {
                var roleExists = await _roleManager.RoleExistsAsync(roleName);

                if (roleExists)
                {
                    _logger.LogInformation("[RoleSeedingService] Role '{Role}' already exists", roleName);
                    continue;
                }

                // Role doesn't exist - create it
                _logger.LogInformation("[RoleSeedingService] Creating role: {Role}", roleName);

                var result = await _roleManager.CreateAsync(new IdentityRole(roleName));

                if (result.Succeeded)
                {
                    _logger.LogInformation("[RoleSeedingService] Successfully created role: {Role}", roleName);
                }
                else
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    _logger.LogError("[RoleSeedingService] Failed to create role '{Role}': {Errors}", roleName, errors);
                }
            }

            _logger.LogInformation("[RoleSeedingService] Role seeding process completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoleSeedingService] Unexpected error during role seeding");
            throw;
        }
    }
}
