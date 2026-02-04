using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Admin;

/// <summary>
/// HTTP client for admin audit log API endpoints.
/// Admin-only access to view authentication audit trail.
/// </summary>
public sealed class AuditLogsApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    /// <summary>
    /// Query audit logs with filtering and pagination.
    /// </summary>
    /// <param name="request">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated audit log results</returns>
    public async Task<AuditLogQueryResponse> QueryAuditLogsAsync(
        AuditLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.UserId))
            queryParams.Add($"userId={Uri.EscapeDataString(request.UserId)}");

        if (!string.IsNullOrWhiteSpace(request.EventType))
            queryParams.Add($"eventType={Uri.EscapeDataString(request.EventType)}");

        if (!string.IsNullOrWhiteSpace(request.Role))
            queryParams.Add($"role={Uri.EscapeDataString(request.Role)}");

        if (request.StartDate.HasValue)
            queryParams.Add($"startDate={request.StartDate.Value:yyyy-MM-dd}");

        if (request.EndDate.HasValue)
            queryParams.Add($"endDate={request.EndDate.Value:yyyy-MM-dd}");

        queryParams.Add($"page={request.Page}");
        queryParams.Add($"pageSize={request.PageSize}");
        queryParams.Add($"sortOrder={request.SortOrder}");

        var queryString = string.Join("&", queryParams);
        var url = $"api/admin/audit-logs?{queryString}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuditLogQueryResponse>(cancellationToken: cancellationToken);
        return result ?? new AuditLogQueryResponse();
    }

    /// <summary>
    /// Get available event types for filtering.
    /// </summary>
    public async Task<string[]> GetEventTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/admin/audit-logs/event-types", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<string[]>(cancellationToken: cancellationToken);
        return result ?? Array.Empty<string>();
    }

    /// <summary>
    /// Get available roles for filtering.
    /// </summary>
    public async Task<string[]> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/admin/audit-logs/roles", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<string[]>(cancellationToken: cancellationToken);
        return result ?? Array.Empty<string>();
    }
}
