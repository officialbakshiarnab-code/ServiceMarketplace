using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class MessageReport : BaseAuditableEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public string ReporterUserId { get; set; } = string.Empty;
    public string ReportedSenderUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }

    public MessageReportStatus Status { get; set; } = MessageReportStatus.PendingReview;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public bool MessageHidden { get; set; }
    public bool ConversationRestricted { get; set; }
    public DateTime? LegalHoldUntil { get; set; }
}
