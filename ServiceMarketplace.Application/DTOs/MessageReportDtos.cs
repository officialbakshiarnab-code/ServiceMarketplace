using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public sealed class CreateMessageReportDto
{
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public sealed class ReviewMessageReportDto
{
    public MessageReportStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
    public bool HideMessage { get; set; }
    public bool RestrictConversation { get; set; }
    public DateTime? LegalHoldUntil { get; set; }
}

public sealed class MessageReportDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public string ConversationSubject { get; set; } = string.Empty;
    public string ReporterUserId { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string ReportedSenderUserId { get; set; } = string.Empty;
    public string ReportedSenderName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string MessagePreview { get; set; } = string.Empty;
    public MessageReportStatus Status { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public bool MessageHidden { get; set; }
    public bool ConversationRestricted { get; set; }
    public DateTime? LegalHoldUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}
