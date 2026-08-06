namespace ServiceMarketplace.Domain.Enums;

/// <summary>
/// User type enumeration for classifying users by their primary account type.
/// Backing type is short to ensure SMALLINT storage in the database.
/// </summary>
public enum UserType : short
{
    /// <summary>
    /// Customer (regular user) who can create service requests.
    /// Value: 1
    /// </summary>
    Customer = 1,

    /// <summary>
    /// Service provider who can browse requests and place bids.
    /// Value: 2
    /// </summary>
    Provider = 2,

    /// <summary>
    /// Administrator account with elevated privileges.
    /// Value: 3
    /// </summary>
    Admin = 3
}
