namespace ServiceMarketplace.Application.DTOs;

public class ServiceOrderReviewDto
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Feedback { get; set; }
    public bool IsHidden { get; set; }
    public string? ModerationNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
