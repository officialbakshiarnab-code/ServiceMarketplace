namespace ServiceMarketplace.Application.DTOs;

public class ServiceOrderMessageDto
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
