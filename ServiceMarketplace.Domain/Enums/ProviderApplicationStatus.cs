namespace ServiceMarketplace.Domain.Enums;

public enum ProviderApplicationStatus : short
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Approved = 4,
    MoreInformationRequired = 5,
    Rejected = 6,
    Suspended = 7,
    Revoked = 8
}
