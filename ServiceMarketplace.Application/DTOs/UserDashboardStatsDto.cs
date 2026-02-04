namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// User dashboard statistics.
/// Provides counts of requests and bids for the current user.
/// </summary>
public class UserDashboardStatsDto
{
    /// <summary>
    /// Number of open service requests created by the user.
    /// Status = Open (accepting bids).
    /// </summary>
    public int OpenRequestsCount { get; set; }

    /// <summary>
    /// Number of active bids placed by service providers on user's requests.
    /// Includes all bids on open requests.
    /// </summary>
    public int ActiveBidsCount { get; set; }

    /// <summary>
    /// Number of completed service requests created by the user.
    /// Status = Closed (no longer accepting bids, service completed or cancelled).
    /// </summary>
    public int CompletedRequestsCount { get; set; }

    /// <summary>
    /// Total number of service requests created by the user (all statuses).
    /// </summary>
    public int TotalRequestsCount { get; set; }
}
