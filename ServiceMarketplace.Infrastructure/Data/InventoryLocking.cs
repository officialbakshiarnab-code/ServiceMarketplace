using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Domain.Entities;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>PostgreSQL row locks held by the owning workflow until commit/rollback.</summary>
public static class InventoryLocking
{
    public static IQueryable<ProductListing> LockProductListing(this AppDbContext context, Guid id)
    {
        if (!context.Database.IsRelational()) return context.ProductListings.Where(p => p.Id == id);
        RequireTransaction(context);
        return context.ProductListings.FromSqlInterpolated($"SELECT * FROM \"ProductListings\" WHERE \"Id\" = {id} FOR UPDATE");
    }

    public static IQueryable<ProductDeliveryOrder> LockProductDeliveryOrder(this AppDbContext context, Guid id)
    {
        if (!context.Database.IsRelational()) return context.ProductDeliveryOrders.Where(o => o.Id == id);
        RequireTransaction(context);
        return context.ProductDeliveryOrders.FromSqlInterpolated($"SELECT * FROM \"ProductDeliveryOrders\" WHERE \"Id\" = {id} FOR UPDATE");
    }

    private static void RequireTransaction(AppDbContext context)
    {
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Inventory locking requires an active PostgreSQL transaction.");
    }
}
