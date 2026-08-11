namespace ServiceMarketplace.Domain.Entities;

public class ServiceOrderReview : BaseAuditableEntity
{
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Feedback { get; set; }
    public bool IsHidden { get; set; }
    public string? ModerationNotes { get; set; }
}
