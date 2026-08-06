using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.API.Services;

public sealed class RoleSeedingService(
    AppDbContext dbContext,
    ILogger<RoleSeedingService> logger)
{
    public async Task SeedRolesAsync()
    {
        foreach (var roleName in RoleConstants.AllRoles)
        {
            var exists = await dbContext.Roles.AnyAsync(r => r.Name == roleName);
            if (exists)
                continue;

            dbContext.Roles.Add(new ServiceMarketplace.Domain.Entities.Role
            {
                Name = roleName,
                Description = $"{roleName} account role",
                CreatedDate = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "[RoleSeedingService] Ensured custom roles exist: {Roles}",
            string.Join(", ", RoleConstants.AllRoles));
    }
}
