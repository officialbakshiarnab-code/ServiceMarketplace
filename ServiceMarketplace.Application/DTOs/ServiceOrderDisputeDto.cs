using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ServiceOrderDisputeDto
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public Guid? ServiceOrderPaymentId { get; set; }
    public string RaisedByUserId { get; set; } = string.Empty;
    public string AgainstUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public ServiceOrderDisputeStatus Status { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
