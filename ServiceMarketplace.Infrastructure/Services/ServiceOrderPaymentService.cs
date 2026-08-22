using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServiceOrderPaymentService(
    AppDbContext context,
    INotificationService notificationService,
    IServiceOrderAuditService auditService,
    IConversationService conversationService) : IServiceOrderPaymentService
{
    public async Task<ServiceOrderPaymentDto?> GetForOrderAsync(Guid orderId, string userId)
    {
        var order = await GetParticipantOrderAsync(orderId, userId, tracking: false);

        var payment = await context.ServiceOrderPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ServiceOrderId == order.Id);

        return payment == null ? null : ToDto(payment);
    }

    public async Task<ServiceOrderPaymentDto> RecordAsync(Guid orderId, string customerId, RecordServiceOrderPaymentDto dto)
    {
        var order = await GetParticipantOrderAsync(orderId, customerId, tracking: true);
        if (order.CustomerId != customerId)
            throw new ForbiddenException("Only the customer can record payment for this order.");

        if (order.Status != ServiceOrderStatus.ProviderCompleted)
            throw new BadRequestException("Payment can be recorded only after the provider marks the order complete.");

        if (await context.ServiceOrderPayments.AnyAsync(p => p.ServiceOrderId == order.Id))
            throw new BadRequestException("Payment is already recorded for this order.");

        if (dto.Method == PaymentMethod.Platform)
            throw new BadRequestException("Platform payments require server-side verification and are not enabled yet.");

        if (dto.Amount <= 0)
            throw new BadRequestException("Payment amount must be greater than zero.");

        if (dto.Amount != order.AgreedAmount)
            throw new BadRequestException("Payment amount must match the agreed order amount.");

        var payment = new ServiceOrderPayment
        {
            ServiceOrderId = order.Id,
            CustomerId = order.CustomerId,
            ProviderId = order.ProviderId,
            Amount = dto.Amount,
            Method = dto.Method,
            Status = PaymentStatus.Held,
            ReferenceNumber = NormalizeOptional(dto.ReferenceNumber, 200),
            Notes = NormalizeOptional(dto.Notes, 1000),
            RecordedByUserId = customerId,
            RecordedAt = DateTime.UtcNow
        };

        context.ServiceOrderPayments.Add(payment);
        await context.SaveChangesAsync();
        await auditService.RecordAsync(
            order,
            customerId,
            "Customer",
            "ServiceOrderPaymentRecorded",
            null,
            payment.Status.ToString(),
            $"{payment.Method} payment recorded for {payment.Amount:0.00}.");
        await conversationService.AddServiceOrderSystemMessageAsync(
            order.Id,
            "Payment was recorded for this service order.",
            "payment-recorded");
        await notificationService.NotifyServiceOrderPaymentRecordedAsync(payment.Id);

        return ToDto(payment);
    }

    private async Task<ServiceOrder> GetParticipantOrderAsync(Guid orderId, string userId, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var query = context.ServiceOrders
            .Where(o => o.Id == orderId && (o.CustomerId == userId || o.ProviderId == userId));

        if (!tracking)
            query = query.AsNoTracking();

        var order = await query.FirstOrDefaultAsync();
        if (order == null)
            throw new NotFoundException("Service order not found.");

        return order;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length > maxLength)
            throw new BadRequestException($"Value cannot exceed {maxLength} characters.");

        return value;
    }

    private static ServiceOrderPaymentDto ToDto(ServiceOrderPayment payment)
    {
        return new ServiceOrderPaymentDto
        {
            Id = payment.Id,
            ServiceOrderId = payment.ServiceOrderId,
            CustomerId = payment.CustomerId,
            ProviderId = payment.ProviderId,
            PlatformPaymentIntentId = payment.PlatformPaymentIntentId,
            Amount = payment.Amount,
            PlatformFeeAmount = payment.PlatformFeeAmount,
            ProviderPayoutAmount = payment.ProviderPayoutAmount,
            Method = payment.Method,
            Status = payment.Status,
            ReferenceNumber = payment.ReferenceNumber,
            Notes = payment.Notes,
            RecordedByUserId = payment.RecordedByUserId,
            RecordedAt = payment.RecordedAt,
            ReleasedAt = payment.ReleasedAt,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };
    }
}
