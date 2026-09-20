using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ProductDeliveryOrderService(
    AppDbContext context,
    INotificationService notificationService) : IProductDeliveryOrderService
{
    public async Task<ProductDeliveryOrderDto> CreateAsync(string buyerId, CreateProductDeliveryOrderDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(buyerId))
                throw new ForbiddenException("Product buyer access is required.");

            ValidateCreateDto(dto);

            var listing = await context.LockProductListing(dto.ProductListingId)
                .Include(p => p.SellerProfile)
                .Include(p => p.ProductCategory)
                .FirstOrDefaultAsync(p => p.Id == dto.ProductListingId)
                ?? throw new NotFoundException("Product listing not found.");

            if (listing.Status != ProductListingStatus.Active ||
                listing.StockQuantity <= 0 ||
                !listing.ProductCategory.IsActive ||
                listing.SellerProfile.Status != SellerApplicationStatus.Approved)
            {
                throw new BadRequestException("Selected product is not available for delivery order.");
            }

            if (Guid.TryParse(buyerId, out var buyerGuid) && buyerGuid == listing.SellerId)
                throw new BadRequestException("Sellers cannot order their own products.");

            if (dto.Quantity > listing.StockQuantity)
                throw new BadRequestException("Requested quantity is greater than available stock.");

            if (dto.ServiceZoneId.HasValue)
            {
                var zoneExists = await context.ServiceZones
                    .AsNoTracking()
                    .AnyAsync(z => z.Id == dto.ServiceZoneId.Value && z.IsActive);

                if (!zoneExists)
                    throw new BadRequestException("Selected delivery zone is not available.");
            }

            var order = new ProductDeliveryOrder
            {
                ProductListingId = listing.Id,
                SellerId = listing.SellerId,
                BuyerId = buyerId,
                Quantity = dto.Quantity,
                UnitPrice = listing.Price,
                TotalPrice = listing.Price * dto.Quantity,
                Status = ProductDeliveryStatus.PendingSellerConfirmation,
                DeliveryRecipientName = NormalizeRequired(dto.DeliveryRecipientName, 150, "Delivery recipient name"),
                DeliveryPhoneNumber = NormalizeRequired(dto.DeliveryPhoneNumber, 20, "Delivery phone number"),
                DeliveryAddress = NormalizeRequired(dto.DeliveryAddress, 500, "Delivery address"),
                DeliveryCity = NormalizeRequired(dto.DeliveryCity, 100, "Delivery city"),
                DeliveryState = NormalizeRequired(dto.DeliveryState, 100, "Delivery state"),
                ServiceZoneId = dto.ServiceZoneId,
                BuyerNotes = NormalizeOptional(dto.BuyerNotes, 1000),
                CreatedAt = DateTime.UtcNow
            };

            listing.StockQuantity -= dto.Quantity;
            listing.UpdatedAt = DateTime.UtcNow;

            context.ProductDeliveryOrders.Add(order);
            await context.SaveChangesAsync();
            await notificationService.NotifyProductDeliveryOrderCreatedAsync(order.Id);

            return await GetDtoByIdAsync(order.Id);
        });
    }

    public async Task<List<ProductDeliveryOrderDto>> GetMineAsBuyerAsync(string buyerId)
    {
        if (string.IsNullOrWhiteSpace(buyerId))
            return [];

        return await ToDtoListAsync(context.ProductDeliveryOrders
            .AsNoTracking()
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt));
    }

    public async Task<List<ProductDeliveryOrderDto>> GetMineAsSellerAsync(string sellerId)
    {
        if (!Guid.TryParse(sellerId, out var sellerGuid))
            return [];

        return await ToDtoListAsync(context.ProductDeliveryOrders
            .AsNoTracking()
            .Where(o => o.SellerId == sellerGuid)
            .OrderByDescending(o => o.CreatedAt));
    }

    public async Task<ProductDeliveryOrderDto> UpdateSellerStatusAsync(Guid orderId, string sellerId, UpdateProductDeliveryStatusDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            if (!Guid.TryParse(sellerId, out var sellerGuid))
                throw new ForbiddenException("Product seller access is required.");

            if (!Enum.IsDefined(dto.Status))
                throw new BadRequestException("Selected delivery status is not valid.");

            var order = await context.LockProductDeliveryOrder(orderId)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.SellerId == sellerGuid)
                ?? throw new NotFoundException("Product delivery order not found.");

            order.ProductListing = await context.LockProductListing(order.ProductListingId).SingleAsync();

            ApplySellerTransition(order, dto.Status, sellerId, dto.CancellationReason);

            await context.SaveChangesAsync();
            await notificationService.NotifyProductDeliveryOrderStatusChangedAsync(order.Id);

            return await GetDtoByIdAsync(order.Id);
        });
    }

    public async Task<ProductDeliveryOrderDto> CancelAsBuyerAsync(Guid orderId, string buyerId, string? cancellationReason)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var order = await context.LockProductDeliveryOrder(orderId)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerId == buyerId)
                ?? throw new NotFoundException("Product delivery order not found.");

            if (order.Status is not ProductDeliveryStatus.PendingSellerConfirmation and not ProductDeliveryStatus.Confirmed)
                throw new BadRequestException("This product delivery order can no longer be cancelled by the buyer.");

            order.ProductListing = await context.LockProductListing(order.ProductListingId).SingleAsync();

            CancelOrder(order, buyerId, cancellationReason);

            await context.SaveChangesAsync();
            await notificationService.NotifyProductDeliveryOrderStatusChangedAsync(order.Id);

            return await GetDtoByIdAsync(order.Id);
        });
    }

    private static void ApplySellerTransition(
        ProductDeliveryOrder order,
        ProductDeliveryStatus targetStatus,
        string sellerId,
        string? cancellationReason)
    {
        if (targetStatus == order.Status)
            return;

        if (targetStatus == ProductDeliveryStatus.Cancelled)
        {
            if (order.Status is ProductDeliveryStatus.OutForDelivery or ProductDeliveryStatus.Delivered or ProductDeliveryStatus.Cancelled)
                throw new BadRequestException("This product delivery order can no longer be cancelled by the seller.");

            CancelOrder(order, sellerId, cancellationReason);
            return;
        }

        var now = DateTime.UtcNow;
        switch (order.Status, targetStatus)
        {
            case (ProductDeliveryStatus.PendingSellerConfirmation, ProductDeliveryStatus.Confirmed):
                order.Status = targetStatus;
                order.ConfirmedAt = now;
                break;
            case (ProductDeliveryStatus.Confirmed, ProductDeliveryStatus.ReadyForPickup):
                order.Status = targetStatus;
                order.ReadyForPickupAt = now;
                break;
            case (ProductDeliveryStatus.ReadyForPickup, ProductDeliveryStatus.OutForDelivery):
                order.Status = targetStatus;
                order.OutForDeliveryAt = now;
                break;
            case (ProductDeliveryStatus.OutForDelivery, ProductDeliveryStatus.Delivered):
                order.Status = targetStatus;
                order.DeliveredAt = now;
                break;
            default:
                throw new BadRequestException($"Cannot move product delivery order from {order.Status} to {targetStatus}.");
        }

        order.UpdatedAt = now;
    }

    private static void CancelOrder(ProductDeliveryOrder order, string cancelledByUserId, string? cancellationReason)
    {
        var reason = NormalizeOptional(cancellationReason, 1000);
        if (string.IsNullOrWhiteSpace(reason))
            reason = "Cancelled by participant.";

        if (order.Status != ProductDeliveryStatus.Cancelled && order.ProductListing != null)
        {
            order.ProductListing.StockQuantity += order.Quantity;
            order.ProductListing.UpdatedAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        order.Status = ProductDeliveryStatus.Cancelled;
        order.CancellationReason = reason;
        order.CancelledByUserId = cancelledByUserId;
        order.CancelledAt = now;
        order.UpdatedAt = now;
    }

    private async Task<ProductDeliveryOrderDto> GetDtoByIdAsync(Guid orderId)
    {
        var orders = await ToDtoListAsync(context.ProductDeliveryOrders
            .AsNoTracking()
            .Where(o => o.Id == orderId));

        return orders.Single();
    }

    private async Task<List<ProductDeliveryOrderDto>> ToDtoListAsync(IQueryable<ProductDeliveryOrder> query)
    {
        var orders = await query
            .Select(order => new ProductDeliveryOrderDto
            {
                Id = order.Id,
                ProductListingId = order.ProductListingId,
                ProductTitle = order.ProductListing.Title,
                ProductImageUrl = order.ProductListing.ImageUrl,
                ProductCondition = order.ProductListing.Condition,
                ProductCategoryId = order.ProductListing.ProductCategoryId,
                ProductCategoryName = order.ProductListing.ProductCategory.Name,
                SellerId = order.SellerId.ToString(),
                StoreName = order.SellerProfile.StoreName,
                SellerPickupAddress = order.SellerProfile.PickupAddress,
                BuyerId = order.BuyerId,
                Quantity = order.Quantity,
                UnitPrice = order.UnitPrice,
                TotalPrice = order.TotalPrice,
                Status = order.Status,
                DeliveryRecipientName = order.DeliveryRecipientName,
                DeliveryPhoneNumber = order.DeliveryPhoneNumber,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryCity = order.DeliveryCity,
                DeliveryState = order.DeliveryState,
                ServiceZoneId = order.ServiceZoneId,
                ServiceZoneName = order.ServiceZone == null ? null : order.ServiceZone.DisplayName,
                BuyerNotes = order.BuyerNotes,
                CancellationReason = order.CancellationReason,
                CancelledByUserId = order.CancelledByUserId,
                ConfirmedAt = order.ConfirmedAt,
                ReadyForPickupAt = order.ReadyForPickupAt,
                OutForDeliveryAt = order.OutForDeliveryAt,
                DeliveredAt = order.DeliveredAt,
                CancelledAt = order.CancelledAt,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            })
            .ToListAsync();

        foreach (var order in orders)
        {
            order.ProductConditionLabel = ToConditionLabel(order.ProductCondition);
            order.StatusLabel = ToStatusLabel(order.Status);
        }

        return orders;
    }

    private static void ValidateCreateDto(CreateProductDeliveryOrderDto dto)
    {
        if (dto.ProductListingId == Guid.Empty)
            throw new BadRequestException("Product listing is required.");

        if (dto.Quantity <= 0 || dto.Quantity > 20)
            throw new BadRequestException("Quantity must be between 1 and 20.");

        _ = NormalizeRequired(dto.DeliveryRecipientName, 150, "Delivery recipient name");
        _ = NormalizeRequired(dto.DeliveryPhoneNumber, 20, "Delivery phone number");
        _ = NormalizeRequired(dto.DeliveryAddress, 500, "Delivery address");
        _ = NormalizeRequired(dto.DeliveryCity, 100, "Delivery city");
        _ = NormalizeRequired(dto.DeliveryState, 100, "Delivery state");
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} is required.");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new BadRequestException($"{fieldName} cannot exceed {maxLength} characters.");

        return value;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string ToConditionLabel(ProductCondition condition) => condition switch
    {
        ProductCondition.New => "New",
        ProductCondition.LikeNew => "Like New",
        ProductCondition.Good => "Good",
        ProductCondition.Fair => "Fair",
        ProductCondition.NeedsRepair => "Needs Repair",
        _ => condition.ToString()
    };

    private static string ToStatusLabel(ProductDeliveryStatus status) => status switch
    {
        ProductDeliveryStatus.PendingSellerConfirmation => "Pending Seller Confirmation",
        ProductDeliveryStatus.Confirmed => "Confirmed",
        ProductDeliveryStatus.ReadyForPickup => "Ready For Pickup",
        ProductDeliveryStatus.OutForDelivery => "Out For Delivery",
        ProductDeliveryStatus.Delivered => "Delivered",
        ProductDeliveryStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
