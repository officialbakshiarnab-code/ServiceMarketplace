using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class SellerApplicationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Description { get; set; }
    public string? Gstin { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public string? ServiceZoneName { get; set; }
    public bool IdentityVerificationSubmitted { get; set; }
    public bool AddressVerificationSubmitted { get; set; }
    public bool BusinessVerificationSubmitted { get; set; }
    public SellerApplicationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
}
