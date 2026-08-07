using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class RecordServiceOrderPaymentDto
{
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}
