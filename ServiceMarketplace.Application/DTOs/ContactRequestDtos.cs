using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class CreateContactRequestDto
{
    public string TargetUserId { get; set; } = string.Empty;
    public ContactProfileType TargetProfileType { get; set; }
    public ContactRequestKind Kind { get; set; }
    public string? Message { get; set; }
    public DateTime? PreferredCallbackAt { get; set; }
}

public class ReviewContactRequestDto
{
    public ContactRequestStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
}

public class ContactRequestDto
{
    public Guid Id { get; set; }
    public string RequesterUserId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public ContactProfileType TargetProfileType { get; set; }
    public ContactRequestKind Kind { get; set; }
    public ContactRequestStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime? PreferredCallbackAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContactRequestSummaryDto
{
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int CallbackPendingCount { get; set; }
}
