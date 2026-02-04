using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

/// <summary>
/// Service for retrieving admin dashboard KPI metrics.
/// Provides system-wide statistics for monitoring and analytics.
/// </summary>
public interface IAdminKpiService
{
    /// <summary>
    /// Get comprehensive dashboard KPI metrics for admin users.
    /// Includes user counts, request stats, bid stats, auth stats, and recent events.
    /// Uses efficient queries to minimize database roundtrips.
    /// </summary>
    /// <returns>Admin dashboard KPI metrics</returns>
    Task<AdminDashboardKpiDto> GetDashboardKpisAsync();
}
