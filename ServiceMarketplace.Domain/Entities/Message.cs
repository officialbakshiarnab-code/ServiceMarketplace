using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class Message : BaseAuditableEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string SenderUserId { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.Text;
    public string Body { get; set; } = string.Empty;
    public string? ClientMessageId { get; set; }
    public bool IsHidden { get; set; }
    public string? HiddenByUserId { get; set; }
    public DateTime? HiddenAt { get; set; }
    public string? HiddenReason { get; set; }
}
