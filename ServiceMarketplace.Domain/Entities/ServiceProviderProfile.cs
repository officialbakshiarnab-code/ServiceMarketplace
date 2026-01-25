namespace ServiceMarketplace.Domain.Entities;

public class ServiceProviderProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string Skills { get; set; } = null!;
    public decimal HourlyRate { get; set; }
    public bool IsAvailable { get; set; }
}
