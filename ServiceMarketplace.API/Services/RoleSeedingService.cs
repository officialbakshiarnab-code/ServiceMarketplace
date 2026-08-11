using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Domain.Entities;
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

        await SeedServiceCatalogAsync();

        logger.LogInformation(
            "[RoleSeedingService] Ensured custom roles exist: {Roles}",
            string.Join(", ", RoleConstants.AllRoles));
    }

    private async Task SeedServiceCatalogAsync()
    {
        var categories = new[]
        {
            new ServiceCategory
            {
                Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                Name = "Plumbing",
                Slug = "plumbing",
                Description = "Leaks, taps, pipes, fittings, and water-flow issues",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
            },
            new ServiceCategory
            {
                Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                Name = "Electrical",
                Slug = "electrical",
                Description = "Wiring, fixtures, switchboards, fans, and basic electrical repairs",
                IsActive = true,
                SortOrder = 20,
                CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
            },
            new ServiceCategory
            {
                Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                Name = "Cleaning",
                Slug = "cleaning",
                Description = "Home and small-office cleaning services",
                IsActive = true,
                SortOrder = 30,
                CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        foreach (var category in categories)
        {
            var exists = await dbContext.ServiceCategories.AnyAsync(c => c.Id == category.Id || c.Slug == category.Slug);
            if (!exists)
                dbContext.ServiceCategories.Add(category);
        }

        var zones = new[]
        {
            new ServiceZone
            {
                Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
                Country = "India",
                State = "West Bengal",
                City = "Kolkata",
                ZoneName = "Central Kolkata",
                DisplayName = "Central Kolkata, Kolkata, West Bengal",
                PinCodeRegion = "7000xx",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
            },
            new ServiceZone
            {
                Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
                Country = "India",
                State = "West Bengal",
                City = "Kolkata",
                ZoneName = "South Kolkata",
                DisplayName = "South Kolkata, Kolkata, West Bengal",
                PinCodeRegion = "7000xx",
                IsActive = true,
                SortOrder = 20,
                CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
            },
            new ServiceZone
            {
                Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"),
                Country = "India",
                State = "West Bengal",
                City = "Kolkata",
                ZoneName = "North Kolkata",
                DisplayName = "North Kolkata, Kolkata, West Bengal",
                PinCodeRegion = "7000xx",
                IsActive = true,
                SortOrder = 30,
                CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        foreach (var zone in zones)
        {
            var exists = await dbContext.ServiceZones.AnyAsync(z => z.Id == zone.Id || z.DisplayName == zone.DisplayName);
            if (!exists)
                dbContext.ServiceZones.Add(zone);
        }

        await dbContext.SaveChangesAsync();
    }
}
