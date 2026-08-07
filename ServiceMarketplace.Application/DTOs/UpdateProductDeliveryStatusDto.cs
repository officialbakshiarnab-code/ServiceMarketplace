using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class UpdateProductDeliveryStatusDto
{
    public ProductDeliveryStatus Status { get; set; }
    public string? CancellationReason { get; set; }
}
