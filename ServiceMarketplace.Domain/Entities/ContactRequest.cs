using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ContactRequest : BaseAuditableEntity
{
    public string RequesterUserId { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public ContactProfileType TargetProfileType { get; set; }
    public ContactRequestKind Kind { get; set; }
    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.PendingAdminReview;
    public string? Message { get; set; }
    public DateTime? PreferredCallbackAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public string? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
}
