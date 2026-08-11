namespace ServiceMarketplace.Domain.Entities;

public class ServiceOrderMessage : BaseAuditableEntity
{
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public string SenderUserId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? ReadAt { get; set; }
}
