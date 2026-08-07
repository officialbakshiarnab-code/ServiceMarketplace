namespace ServiceMarketplace.Application.DTOs;

public class ServiceOrderAuditEventDto
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public Guid ServiceRequestId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
