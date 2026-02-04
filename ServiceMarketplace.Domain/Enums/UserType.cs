namespace ServiceMarketplace.Domain.Enums;

/// <summary>
/// User type enumeration for classifying users by their role(s) in the system.
/// Stored as INT in the database for efficiency and flexibility.
/// </summary>
public enum UserType
{
    /// <summary>
    /// Regular user who can create service requests and accept bids.
    /// Value: 1
    /// </summary>
    User = 1,

    /// <summary>
    /// Service provider who can browse requests and place bids.
    /// Value: 2
    /// </summary>
    Provider = 2,

    /// <summary>
    /// User with both roles - can create requests and place bids.
    /// Value: 3
    /// </summary>
    Both = 3
}
