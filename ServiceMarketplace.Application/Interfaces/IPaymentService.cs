namespace ServiceMarketplace.Application.Interfaces;

public interface IPaymentService
{
    Task InitiatePaymentAsync(Guid serviceRequestId, Guid bidId);
    Task ReleasePaymentAsync(Guid serviceRequestId);
}
