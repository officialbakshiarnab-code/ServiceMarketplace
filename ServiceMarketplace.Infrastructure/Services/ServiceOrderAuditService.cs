using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServiceOrderAuditService(AppDbContext context) : IServiceOrderAuditService
{
    public async Task RecordAsync(
        ServiceOrder order,
        string actorUserId,
        string actorRole,
        string eventType,
        string? fromStatus = null,
        string? toStatus = null,
        string? details = null)
    {
        if (order.Id == Guid.Empty || string.IsNullOrWhiteSpace(eventType))
            return;

        context.ServiceOrderAuditEvents.Add(new ServiceOrderAuditEvent
        {
            ServiceOrderId = order.Id,
            ServiceRequestId = order.ServiceRequestId,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            EventType = eventType,
            FromStatus = NormalizeOptional(fromStatus, 100),
            ToStatus = NormalizeOptional(toStatus, 100),
            Details = NormalizeOptional(details, 1000)
        });

        await context.SaveChangesAsync();
    }

    public async Task<List<ServiceOrderAuditEventDto>> GetForOrderAsync(Guid orderId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var canAccess = await context.ServiceOrders
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId && (o.CustomerId == userId || o.ProviderId == userId));

        if (!canAccess)
            throw new NotFoundException("Service order not found.");

        return await context.ServiceOrderAuditEvents
            .AsNoTracking()
            .Where(e => e.ServiceOrderId == orderId)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new ServiceOrderAuditEventDto
            {
                Id = e.Id,
                ServiceOrderId = e.ServiceOrderId,
                ServiceRequestId = e.ServiceRequestId,
                ActorUserId = e.ActorUserId,
                ActorRole = e.ActorRole,
                EventType = e.EventType,
                FromStatus = e.FromStatus,
                ToStatus = e.ToStatus,
                Details = e.Details,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
