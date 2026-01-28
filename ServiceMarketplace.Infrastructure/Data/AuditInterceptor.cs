using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ServiceMarketplace.Domain.Entities;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>
/// EF Core interceptor that automatically populates audit fields on save.
/// - Sets CreatedAt on new entities
/// - Sets UpdatedAt on modified entities
/// All timestamps use UTC.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Updates audit fields (CreatedAt, UpdatedAt) for all tracked BaseAuditableEntity instances.
    /// Called automatically before SaveChanges/SaveChangesAsync.
    /// </summary>
    /// <param name="context">The DbContext being saved</param>
    private static void UpdateAuditFields(DbContext? context)
    {
        if (context == null) return;

        // Get current UTC timestamp once for consistency
        var now = DateTime.UtcNow;

        // Get all tracked auditable entities in modified or added state
        var entries = context.ChangeTracker
            .Entries<BaseAuditableEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                // New entity - set creation timestamp
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Modified entity - update modification timestamp
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
