namespace ServiceMarketplace.Application.DTOs;

public class VerifyPlatformPaymentIntentDto
{
    public string GatewayPaymentId { get; set; } = string.Empty;
    public string? VerificationNotes { get; set; }
}
