namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Contract for payment orchestration (future or external provider).
public interface IPaymentService
{
    Task InitiatePaymentAsync(Guid serviceRequestId, Guid bidId);
    Task ReleasePaymentAsync(Guid serviceRequestId);
}
