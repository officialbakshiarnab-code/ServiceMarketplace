using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Admin;

/// <summary>
/// HTTP client for admin KPI API endpoints.
/// Admin-only access to system-wide metrics and statistics.
/// </summary>
public sealed class AdminKpiApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    /// <summary>
    /// Get comprehensive dashboard KPI metrics.
    /// Includes user counts, request stats, bid stats, auth stats, and recent events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Admin dashboard KPI metrics</returns>
    public async Task<AdminDashboardKpiDto> GetDashboardKpisAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/admin/kpis", cancellationToken);
        response.EnsureSuccessStatusCode();

        var kpis = await response.Content.ReadFromJsonAsync<AdminDashboardKpiDto>(cancellationToken: cancellationToken);
        return kpis ?? new AdminDashboardKpiDto();
    }
}
