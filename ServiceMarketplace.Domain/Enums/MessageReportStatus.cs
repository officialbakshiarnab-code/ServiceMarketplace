namespace ServiceMarketplace.Domain.Enums;

public enum MessageReportStatus : short
{
    PendingReview = 1,
    ReviewedNoAction = 2,
    ActionTaken = 3,
    Dismissed = 4
}
