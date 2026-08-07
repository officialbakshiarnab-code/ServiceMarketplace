using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Entities;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServiceOrderAuditService
{
    Task RecordAsync(
        ServiceOrder order,
        string actorUserId,
        string actorRole,
        string eventType,
        string? fromStatus = null,
        string? toStatus = null,
        string? details = null);

    Task<List<ServiceOrderAuditEventDto>> GetForOrderAsync(Guid orderId, string userId);
}
