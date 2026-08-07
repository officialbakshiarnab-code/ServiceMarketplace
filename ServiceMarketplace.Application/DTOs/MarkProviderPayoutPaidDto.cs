namespace ServiceMarketplace.Application.DTOs;

public class MarkProviderPayoutPaidDto
{
    public string PayoutReference { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
