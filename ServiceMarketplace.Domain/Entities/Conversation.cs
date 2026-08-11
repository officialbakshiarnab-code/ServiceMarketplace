using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class Conversation : BaseAuditableEntity
{
    public ConversationContextType ContextType { get; set; } = ConversationContextType.ServiceOrder;
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
    public Guid? ServiceOrderId { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public Guid? LastMessageId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? RetainUntil { get; set; }
    public DateTime? LegalHoldUntil { get; set; }
    public string? RetentionNotes { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
