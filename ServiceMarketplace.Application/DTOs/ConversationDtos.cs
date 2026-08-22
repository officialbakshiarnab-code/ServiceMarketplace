using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public sealed class ConversationInboxItemDto
{
    public Guid Id { get; set; }
    public ConversationContextType ContextType { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public ConversationStatus Status { get; set; }
    public string OtherParticipantName { get; set; } = string.Empty;
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class ConversationDetailDto
{
    public Guid Id { get; set; }
    public ConversationContextType ContextType { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public ConversationStatus Status { get; set; }
    public IReadOnlyList<ConversationParticipantDto> Participants { get; set; } = [];
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ConversationParticipantDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ConversationParticipantKind ParticipantKind { get; set; }
}

public sealed class ConversationMessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsMine { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SendConversationMessageDto
{
    public string Body { get; set; } = string.Empty;
    public string? ClientMessageId { get; set; }
}

public sealed class ConversationMessagesPageDto
{
    public IReadOnlyList<ConversationMessageDto> Items { get; set; } = [];
    public DateTime? NextBefore { get; set; }
}

public sealed class UpdateConversationPreferencesDto
{
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class ConversationUnreadCountDto
{
    public int Count { get; set; }
}

public sealed class ConversationReadResultDto
{
    public Guid ConversationId { get; set; }
    public Guid? LastReadMessageId { get; set; }
    public DateTime? LastReadAt { get; set; }
    public int UnreadCount { get; set; }
}

public sealed class ConversationRealtimeUpdateDto
{
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public IReadOnlyList<string> ParticipantUserIds { get; set; } = [];
}
