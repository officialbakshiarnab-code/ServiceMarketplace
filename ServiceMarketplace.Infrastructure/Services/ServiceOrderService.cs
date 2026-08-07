using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServiceOrderService(
    AppDbContext context,
    INotificationService notificationService,
    IServiceOrderAuditService auditService) : IServiceOrderService
{
    public async Task<List<ServiceOrderDto>> GetForCustomerAsync(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return [];

        var orders = await BaseQuery()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return await ToDtosAsync(orders);
    }

    public async Task<List<ServiceOrderDto>> GetForProviderAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return [];

        var orders = await BaseQuery()
            .Where(o => o.ProviderId == providerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return await ToDtosAsync(orders);
    }

    public async Task<ServiceOrderDto> GetByIdAsync(Guid orderId, string userId)
    {
        var order = await GetParticipantOrderAsync(orderId, userId, tracking: false);
        return await ToDtoAsync(order);
    }

    public async Task<ServiceOrderDto> StartAsync(Guid orderId, string providerId)
    {
        var order = await GetParticipantOrderAsync(orderId, providerId, tracking: true);
        if (order.ProviderId != providerId)
            throw new ForbiddenException("Only the accepted provider can start this order.");

        if (order.Status != ServiceOrderStatus.PendingStart)
            throw new BadRequestException("Only pending orders can be started.");

        var fromStatus = order.Status;
        order.Status = ServiceOrderStatus.InProgress;
        order.StartedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await auditService.RecordAsync(
            order,
            providerId,
            "Provider",
            "ServiceOrderStarted",
            fromStatus.ToString(),
            order.Status.ToString());
        await notificationService.NotifyServiceOrderStartedAsync(order.Id);
        return await ToDtoAsync(order);
    }

    public async Task<ServiceOrderDto> MarkProviderCompletedAsync(Guid orderId, string providerId)
    {
        var order = await GetParticipantOrderAsync(orderId, providerId, tracking: true);
        if (order.ProviderId != providerId)
            throw new ForbiddenException("Only the accepted provider can mark this order complete.");

        if (order.Status != ServiceOrderStatus.InProgress)
            throw new BadRequestException("Only in-progress orders can be marked complete by the provider.");

        var fromStatus = order.Status;
        order.Status = ServiceOrderStatus.ProviderCompleted;
        order.ProviderCompletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await auditService.RecordAsync(
            order,
            providerId,
            "Provider",
            "ServiceOrderProviderCompleted",
            fromStatus.ToString(),
            order.Status.ToString());
        await notificationService.NotifyServiceOrderProviderCompletedAsync(order.Id);
        return await ToDtoAsync(order);
    }

    public async Task<ServiceOrderDto> ConfirmCustomerCompletionAsync(Guid orderId, string customerId)
    {
        var order = await GetParticipantOrderAsync(orderId, customerId, tracking: true);
        if (order.CustomerId != customerId)
            throw new ForbiddenException("Only the customer can confirm completion.");

        if (order.Status != ServiceOrderStatus.ProviderCompleted)
            throw new BadRequestException("Only provider-completed orders can be confirmed.");

        var payment = await context.ServiceOrderPayments
            .FirstOrDefaultAsync(p => p.ServiceOrderId == order.Id)
            ?? throw new BadRequestException("Payment must be recorded before completion can be confirmed.");

        if (payment.Status != PaymentStatus.Held)
            throw new BadRequestException("Payment must be recorded and held before completion can be confirmed.");

        var fromStatus = order.Status;
        order.Status = ServiceOrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        order.ServiceRequest.Status = ServiceRequestStatus.Closed;
        payment.Status = PaymentStatus.Released;
        payment.ReleasedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await auditService.RecordAsync(
            order,
            customerId,
            "Customer",
            "ServiceOrderCompleted",
            fromStatus.ToString(),
            order.Status.ToString(),
            $"Payment {payment.Id} released.");
        await notificationService.NotifyServiceOrderCompletedAsync(order.Id);
        return await ToDtoAsync(order);
    }

    public async Task<ServiceOrderDto> CancelAsync(Guid orderId, string userId, CancelServiceOrderDto dto)
    {
        var order = await GetParticipantOrderAsync(orderId, userId, tracking: true);
        if (order.Status is ServiceOrderStatus.Completed or ServiceOrderStatus.Cancelled)
            throw new BadRequestException("Completed or cancelled orders cannot be cancelled.");

        var hasRecordedPayment = await context.ServiceOrderPayments
            .AnyAsync(p => p.ServiceOrderId == order.Id && p.Status != PaymentStatus.Pending);

        if (hasRecordedPayment)
            throw new BadRequestException("Orders with recorded payment cannot be cancelled from this workflow.");

        var reason = NormalizeReason(dto.Reason);

        var fromStatus = order.Status;
        order.Status = ServiceOrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelledByUserId = userId;
        order.CancellationReason = reason;
        order.UpdatedAt = DateTime.UtcNow;

        order.ServiceRequest.Status = ServiceRequestStatus.Closed;

        await context.SaveChangesAsync();
        await auditService.RecordAsync(
            order,
            userId,
            order.CustomerId == userId ? "Customer" : "Provider",
            "ServiceOrderCancelled",
            fromStatus.ToString(),
            order.Status.ToString(),
            reason);
        await notificationService.NotifyServiceOrderCancelledAsync(order.Id, userId);
        return await ToDtoAsync(order);
    }

    private IQueryable<ServiceOrder> BaseQuery()
    {
        return context.ServiceOrders
            .AsNoTracking()
            .Include(o => o.ServiceRequest)
            .Include(o => o.AcceptedBid)
            .Include(o => o.ServicePackage)
            .Include(o => o.Payment)
            .Include(o => o.Review);
    }

    private async Task<ServiceOrder> GetParticipantOrderAsync(Guid orderId, string userId, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var query = context.ServiceOrders
            .Include(o => o.ServiceRequest)
            .Include(o => o.AcceptedBid)
            .Include(o => o.ServicePackage)
            .Include(o => o.Payment)
            .Include(o => o.Review)
            .Where(o => o.Id == orderId && (o.CustomerId == userId || o.ProviderId == userId));

        if (!tracking)
            query = query.AsNoTracking();

        var order = await query.FirstOrDefaultAsync();
        if (order == null)
            throw new NotFoundException("Service order not found.");

        return order;
    }

    private async Task<ServiceOrderDto> ToDtoAsync(ServiceOrder order)
    {
        ServiceProviderProfile? providerProfile = null;
        if (Guid.TryParse(order.ProviderId, out var providerGuid))
        {
            providerProfile = await context.ServiceProviderProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == providerGuid);
        }

        return ToDto(order, providerProfile);
    }

    private async Task<List<ServiceOrderDto>> ToDtosAsync(List<ServiceOrder> orders)
    {
        var providerIds = orders
            .Select(o => Guid.TryParse(o.ProviderId, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var profiles = await context.ServiceProviderProfiles
            .AsNoTracking()
            .Where(p => providerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        return orders
            .Select(order =>
            {
                ServiceProviderProfile? profile = null;
                if (Guid.TryParse(order.ProviderId, out var providerId))
                    profiles.TryGetValue(providerId, out profile);

                return ToDto(order, profile);
            })
            .ToList();
    }

    private static ServiceOrderDto ToDto(ServiceOrder order, ServiceProviderProfile? providerProfile)
    {
        return new ServiceOrderDto
        {
            Id = order.Id,
            ServiceRequestId = order.ServiceRequestId,
            AcceptedBidId = order.AcceptedBidId,
            ServicePackageId = order.ServicePackageId,
            CustomerId = order.CustomerId,
            ProviderId = order.ProviderId,
            RequestTitle = order.ServiceRequest.Title,
            RequestCategory = order.ServiceRequest.Category,
            ProviderDisplayName = providerProfile?.DisplayName ?? "Service Provider",
            ProviderBusinessName = providerProfile?.BusinessName,
            AgreedAmount = order.AgreedAmount,
            ScheduledStartAt = order.ScheduledStartAt,
            EstimatedDurationMinutes = order.EstimatedDurationMinutes,
            ExactLocation = order.ServiceRequest.Location,
            Latitude = order.ServiceRequest.Latitude,
            Longitude = order.ServiceRequest.Longitude,
            Status = order.Status,
            StartedAt = order.StartedAt,
            ProviderCompletedAt = order.ProviderCompletedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            CancelledByUserId = order.CancelledByUserId,
            CancellationReason = order.CancellationReason,
            PaymentMethod = order.Payment?.Method,
            PaymentStatus = order.Payment?.Status,
            PaymentRecordedAt = order.Payment?.RecordedAt,
            PaymentReleasedAt = order.Payment?.ReleasedAt,
            ReviewRating = order.Review?.Rating,
            ReviewFeedback = order.Review?.Feedback,
            ReviewIsHidden = order.Review?.IsHidden,
            ReviewCreatedAt = order.Review?.CreatedAt,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BadRequestException("Cancellation reason is required.");

        reason = reason.Trim();
        if (reason.Length > 1000)
            throw new BadRequestException("Cancellation reason cannot exceed 1000 characters.");

        return reason;
    }
}
