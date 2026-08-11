namespace ServiceMarketplace.Domain.Enums;

public enum ServiceOrderDisputeStatus : short
{
    Open = 1,
    UnderReview = 2,
    RefundedToCustomer = 3,
    ReleasedToProvider = 4,
    Rejected = 5,
    Cancelled = 6
}
