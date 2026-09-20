using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class MarketplaceEconomicsService(
    AppDbContext context,
    INotificationService notificationService,
    IServiceOrderAuditService auditService) : IMarketplaceEconomicsService
{
    private const decimal PlatformFeeRate = 0.10m;

    public async Task<PlatformPaymentIntentDto?> GetPlatformIntentForOrderAsync(Guid orderId, string userId)
    {
        _ = await GetParticipantOrderAsync(orderId, userId, tracking: false);

        var intent = await context.PlatformPaymentIntents
            .AsNoTracking()
            .Where(i => i.ServiceOrderId == orderId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        return intent == null ? null : ToDto(intent);
    }

    public async Task<PlatformPaymentIntentDto> CreatePlatformIntentAsync(Guid orderId, string customerId)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var order = await GetParticipantOrderAsync(orderId, customerId, tracking: true);
            if (order.CustomerId != customerId)
                throw new ForbiddenException("Only the customer can start platform payment.");

            if (order.Status != ServiceOrderStatus.ProviderCompleted)
                throw new BadRequestException("Platform payment can be started only after the provider marks the order complete.");

            if (await context.ServiceOrderPayments.AnyAsync(p => p.ServiceOrderId == order.Id))
                throw new BadRequestException("Payment is already recorded for this order.");

            var hasActiveIntent = await context.PlatformPaymentIntents.AnyAsync(i =>
                i.ServiceOrderId == order.Id &&
                i.Status == PlatformPaymentIntentStatus.PendingVerification &&
                i.ExpiresAt > DateTime.UtcNow);

            if (hasActiveIntent)
                throw new BadRequestException("A platform payment intent is already pending verification for this order.");

            var fee = CalculatePlatformFee(order.AgreedAmount);
            var intent = new PlatformPaymentIntent
            {
                ServiceOrderId = order.Id,
                CustomerId = order.CustomerId,
                ProviderId = order.ProviderId,
                Amount = order.AgreedAmount,
                PlatformFeeAmount = fee,
                ProviderPayoutAmount = order.AgreedAmount - fee,
                Status = PlatformPaymentIntentStatus.PendingVerification,
                GatewayReference = $"SMPI-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            context.PlatformPaymentIntents.Add(intent);
            await context.SaveChangesAsync();
            await auditService.RecordAsync(
                order,
                customerId,
                "Customer",
                "PlatformPaymentIntentCreated",
                null,
                intent.Status.ToString(),
                $"Platform payment intent {intent.GatewayReference} created for {intent.Amount:0.00}.");

            return ToDto(intent);
        });
    }

    // Administrator confirmation of an external payment reference. No gateway is called here.
    public async Task<ServiceOrderPaymentDto> VerifyPlatformIntentAsync(Guid intentId, string adminId, VerifyPlatformPaymentIntentDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var gatewayPaymentId = NormalizeRequired(dto.GatewayPaymentId, 200, "Gateway payment id");

            var intent = await context.PlatformPaymentIntents
                .Include(i => i.ServiceOrder)
                .FirstOrDefaultAsync(i => i.Id == intentId)
                ?? throw new NotFoundException("Platform payment intent not found.");

            if (intent.Status != PlatformPaymentIntentStatus.PendingVerification)
                throw new BadRequestException("Only pending platform payment intents can be verified.");

            if (intent.ServiceOrder.Status != ServiceOrderStatus.ProviderCompleted)
                throw new BadRequestException("Platform payment can be verified only while the order is provider-completed.");

            if (intent.ExpiresAt <= DateTime.UtcNow)
                throw new BadRequestException("Platform payment intent has expired.");

            if (await context.ServiceOrderPayments.AnyAsync(p => p.ServiceOrderId == intent.ServiceOrderId))
                throw new BadRequestException("Payment is already recorded for this order.");

            intent.Status = PlatformPaymentIntentStatus.Verified;
            intent.GatewayPaymentId = gatewayPaymentId;
            intent.VerificationNotes = NormalizeOptional(dto.VerificationNotes, 1000);
            intent.VerifiedByUserId = adminId;
            intent.VerifiedAt = DateTime.UtcNow;
            intent.UpdatedAt = DateTime.UtcNow;

            var payment = new ServiceOrderPayment
            {
                ServiceOrderId = intent.ServiceOrderId,
                PlatformPaymentIntentId = intent.Id,
                CustomerId = intent.CustomerId,
                ProviderId = intent.ProviderId,
                Amount = intent.Amount,
                PlatformFeeAmount = intent.PlatformFeeAmount,
                ProviderPayoutAmount = intent.ProviderPayoutAmount,
                Method = PaymentMethod.Platform,
                Status = PaymentStatus.Held,
                ReferenceNumber = gatewayPaymentId,
                Notes = intent.VerificationNotes,
                RecordedByUserId = adminId,
                RecordedAt = DateTime.UtcNow
            };

            context.ServiceOrderPayments.Add(payment);
            await context.SaveChangesAsync();
            await auditService.RecordAsync(
                intent.ServiceOrder,
                adminId,
                "Admin",
                "PlatformPaymentVerified",
                null,
                payment.Status.ToString(),
                $"Platform payment {gatewayPaymentId} verified and held.");
            await notificationService.NotifyPlatformPaymentVerifiedAsync(payment.Id);

            return ToPaymentDto(payment);
        });
    }

    public async Task<ProviderPayoutDto?> GetPayoutForOrderAsync(Guid orderId, string userId)
    {
        var order = await GetParticipantOrderAsync(orderId, userId, tracking: false);

        var payout = await context.ProviderPayouts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ServiceOrderId == order.Id);

        return payout == null ? null : ToDto(payout);
    }

    public async Task<List<ProviderPayoutDto>> GetPayoutsAsync(ProviderPayoutStatus? status = null)
    {
        var query = context.ProviderPayouts.AsNoTracking();
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<ProviderPayoutDto> MarkPayoutPaidAsync(Guid payoutId, string adminId, MarkProviderPayoutPaidDto dto)
    {
        var payout = await context.ProviderPayouts
            .FirstOrDefaultAsync(p => p.Id == payoutId)
            ?? throw new NotFoundException("Provider payout not found.");

        if (payout.Status != ProviderPayoutStatus.Pending)
            throw new BadRequestException("Only pending payouts can be marked paid.");

        payout.Status = ProviderPayoutStatus.Paid;
        payout.PayoutReference = NormalizeRequired(dto.PayoutReference, 200, "Payout reference");
        payout.Notes = NormalizeOptional(dto.Notes, 1000);
        payout.MarkedPaidByUserId = adminId;
        payout.PaidAt = DateTime.UtcNow;
        payout.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ToDto(payout);
    }

    public async Task<ServiceOrderDisputeDto> CreateDisputeAsync(Guid orderId, string userId, CreateServiceOrderDisputeDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var order = await GetParticipantOrderAsync(orderId, userId, tracking: true);
            var payment = await context.ServiceOrderPayments
                .FirstOrDefaultAsync(p => p.ServiceOrderId == order.Id)
                ?? throw new BadRequestException("A recorded payment is required before opening a dispute.");

            if (payment.Status != PaymentStatus.Held)
                throw new BadRequestException("Disputes can be opened only while payment is held.");

            var hasOpenDispute = await context.ServiceOrderDisputes.AnyAsync(d =>
                d.ServiceOrderId == order.Id &&
                d.Status != ServiceOrderDisputeStatus.RefundedToCustomer &&
                d.Status != ServiceOrderDisputeStatus.ReleasedToProvider &&
                d.Status != ServiceOrderDisputeStatus.Rejected &&
                d.Status != ServiceOrderDisputeStatus.Cancelled);

            if (hasOpenDispute)
                throw new BadRequestException("An active dispute already exists for this order.");

            var dispute = new ServiceOrderDispute
            {
                ServiceOrderId = order.Id,
                ServiceOrderPaymentId = payment.Id,
                RaisedByUserId = userId,
                AgainstUserId = order.CustomerId == userId ? order.ProviderId : order.CustomerId,
                Reason = NormalizeRequired(dto.Reason, 1000, "Dispute reason"),
                Status = ServiceOrderDisputeStatus.Open
            };

            context.ServiceOrderDisputes.Add(dispute);
            await context.SaveChangesAsync();
            await auditService.RecordAsync(
                order,
                userId,
                order.CustomerId == userId ? "Customer" : "Provider",
                "ServiceOrderDisputeOpened",
                null,
                dispute.Status.ToString(),
                dispute.Reason);
            await notificationService.NotifyServiceOrderDisputeOpenedAsync(dispute.Id);

            return ToDto(dispute);
        });
    }

    public async Task<List<ServiceOrderDisputeDto>> GetDisputesForOrderAsync(Guid orderId, string userId)
    {
        _ = await GetParticipantOrderAsync(orderId, userId, tracking: false);

        return await context.ServiceOrderDisputes
            .AsNoTracking()
            .Where(d => d.ServiceOrderId == orderId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => ToDto(d))
            .ToListAsync();
    }

    public async Task<List<ServiceOrderDisputeDto>> GetDisputesAsync(ServiceOrderDisputeStatus? status = null)
    {
        var query = context.ServiceOrderDisputes.AsNoTracking();
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => ToDto(d))
            .ToListAsync();
    }

    public async Task<ServiceOrderDisputeDto> ResolveDisputeAsync(Guid disputeId, string adminId, ResolveServiceOrderDisputeDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var dispute = await context.ServiceOrderDisputes
                .Include(d => d.ServiceOrder)
                .ThenInclude(o => o.ServiceRequest)
                .Include(d => d.ServiceOrderPayment)
                .FirstOrDefaultAsync(d => d.Id == disputeId)
                ?? throw new NotFoundException("Service order dispute not found.");

            if (dispute.Status is ServiceOrderDisputeStatus.RefundedToCustomer or ServiceOrderDisputeStatus.ReleasedToProvider or ServiceOrderDisputeStatus.Rejected or ServiceOrderDisputeStatus.Cancelled)
                throw new BadRequestException("Dispute is already resolved.");

            var resolutionNotes = NormalizeRequired(dto.ResolutionNotes, 1000, "Resolution notes");
            var payment = dispute.ServiceOrderPayment
                ?? throw new BadRequestException("Dispute is not linked to a payment.");
            ProviderPayout? payoutToNotify = null;

            switch (dto.Status)
            {
                case ServiceOrderDisputeStatus.RefundedToCustomer:
                    if (payment.Status != PaymentStatus.Held)
                        throw new BadRequestException("Only held payments can be refunded.");
                    payment.Status = PaymentStatus.Refunded;
                    payment.UpdatedAt = DateTime.UtcNow;
                    CloseOrderAsCancelled(dispute.ServiceOrder, adminId, resolutionNotes);
                    break;
                case ServiceOrderDisputeStatus.ReleasedToProvider:
                    if (payment.Status != PaymentStatus.Held)
                        throw new BadRequestException("Only held payments can be released.");
                    payment.Status = PaymentStatus.Released;
                    payment.ReleasedAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                    CloseOrderAsCompleted(dispute.ServiceOrder);
                    payoutToNotify = await EnsureProviderPayoutAsync(payment);
                    break;
                case ServiceOrderDisputeStatus.Rejected:
                    break;
                default:
                    throw new BadRequestException("Dispute resolution status must refund customer, release provider, or reject dispute.");
            }

            dispute.Status = dto.Status;
            dispute.ResolutionNotes = resolutionNotes;
            dispute.ResolvedByUserId = adminId;
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            if (payoutToNotify != null)
                await notificationService.NotifyProviderPayoutCreatedAsync(payoutToNotify.Id);

            await auditService.RecordAsync(
                dispute.ServiceOrder,
                adminId,
                "Admin",
                "ServiceOrderDisputeResolved",
                null,
                dispute.Status.ToString(),
                resolutionNotes);
            await notificationService.NotifyServiceOrderDisputeResolvedAsync(dispute.Id);

            return ToDto(dispute);
        });
    }

    private async Task<ProviderPayout> EnsureProviderPayoutAsync(ServiceOrderPayment payment)
    {
        var existing = await context.ProviderPayouts
            .FirstOrDefaultAsync(p => p.ServiceOrderPaymentId == payment.Id);

        if (existing != null)
            return existing;

        var fee = payment.PlatformFeeAmount ?? CalculatePlatformFee(payment.Amount);
        var payoutAmount = payment.ProviderPayoutAmount ?? payment.Amount - fee;
        var payout = new ProviderPayout
        {
            ServiceOrderPaymentId = payment.Id,
            ServiceOrderId = payment.ServiceOrderId,
            ProviderId = payment.ProviderId,
            GrossAmount = payment.Amount,
            PlatformFeeAmount = fee,
            PayoutAmount = payoutAmount,
            Status = ProviderPayoutStatus.Pending
        };

        context.ProviderPayouts.Add(payout);
        return payout;
    }

    private static void CloseOrderAsCancelled(ServiceOrder order, string adminId, string reason)
    {
        if (order.Status is ServiceOrderStatus.Completed or ServiceOrderStatus.Cancelled)
            return;

        order.Status = ServiceOrderStatus.Cancelled;
        order.CancelledByUserId = adminId;
        order.CancellationReason = reason;
        order.CancelledAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        order.ServiceRequest.Status = ServiceRequestStatus.Closed;
    }

    private static void CloseOrderAsCompleted(ServiceOrder order)
    {
        if (order.Status == ServiceOrderStatus.Completed)
            return;

        if (order.Status == ServiceOrderStatus.Cancelled)
            throw new BadRequestException("Cancelled orders cannot be released to provider.");

        order.Status = ServiceOrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        order.ServiceRequest.Status = ServiceRequestStatus.Closed;
    }

    private async Task<ServiceOrder> GetParticipantOrderAsync(Guid orderId, string userId, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var query = context.ServiceOrders
            .Include(o => o.ServiceRequest)
            .Where(o => o.Id == orderId && (o.CustomerId == userId || o.ProviderId == userId));

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync()
            ?? throw new NotFoundException("Service order not found.");
    }

    private static decimal CalculatePlatformFee(decimal amount) =>
        Math.Round(amount * PlatformFeeRate, 2, MidpointRounding.AwayFromZero);

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

    private static PlatformPaymentIntentDto ToDto(PlatformPaymentIntent intent) => new()
    {
        Id = intent.Id,
        ServiceOrderId = intent.ServiceOrderId,
        CustomerId = intent.CustomerId,
        ProviderId = intent.ProviderId,
        Amount = intent.Amount,
        PlatformFeeAmount = intent.PlatformFeeAmount,
        ProviderPayoutAmount = intent.ProviderPayoutAmount,
        Status = intent.Status,
        GatewayReference = intent.GatewayReference,
        GatewayPaymentId = intent.GatewayPaymentId,
        VerificationNotes = intent.VerificationNotes,
        FailureReason = intent.FailureReason,
        VerifiedByUserId = intent.VerifiedByUserId,
        ExpiresAt = intent.ExpiresAt,
        VerifiedAt = intent.VerifiedAt,
        CreatedAt = intent.CreatedAt,
        UpdatedAt = intent.UpdatedAt
    };

    private static ProviderPayoutDto ToDto(ProviderPayout payout) => new()
    {
        Id = payout.Id,
        ServiceOrderPaymentId = payout.ServiceOrderPaymentId,
        ServiceOrderId = payout.ServiceOrderId,
        ProviderId = payout.ProviderId,
        GrossAmount = payout.GrossAmount,
        PlatformFeeAmount = payout.PlatformFeeAmount,
        PayoutAmount = payout.PayoutAmount,
        Status = payout.Status,
        PayoutReference = payout.PayoutReference,
        Notes = payout.Notes,
        MarkedPaidByUserId = payout.MarkedPaidByUserId,
        PaidAt = payout.PaidAt,
        CreatedAt = payout.CreatedAt,
        UpdatedAt = payout.UpdatedAt
    };

    private static ServiceOrderDisputeDto ToDto(ServiceOrderDispute dispute) => new()
    {
        Id = dispute.Id,
        ServiceOrderId = dispute.ServiceOrderId,
        ServiceOrderPaymentId = dispute.ServiceOrderPaymentId,
        RaisedByUserId = dispute.RaisedByUserId,
        AgainstUserId = dispute.AgainstUserId,
        Reason = dispute.Reason,
        Status = dispute.Status,
        ResolutionNotes = dispute.ResolutionNotes,
        ResolvedByUserId = dispute.ResolvedByUserId,
        ResolvedAt = dispute.ResolvedAt,
        CreatedAt = dispute.CreatedAt,
        UpdatedAt = dispute.UpdatedAt
    };

    private static ServiceOrderPaymentDto ToPaymentDto(ServiceOrderPayment payment) => new()
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
