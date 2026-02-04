namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Paginated response for audit log queries.
/// </summary>
public class AuditLogQueryResponse
{
    /// <summary>
    /// Audit log entries for the current page.
    /// </summary>
    public List<AuditLogDto> Items { get; set; } = new();

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of records matching the filter.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
