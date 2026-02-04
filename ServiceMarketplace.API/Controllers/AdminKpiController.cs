using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Controllers;

/// <summary>
/// Admin-only controller for dashboard KPI metrics.
/// Provides system-wide statistics for monitoring and analytics.
/// Rate Limited: 200 requests per minute per admin user.
/// </summary>
[ApiController]
[Route("api/admin/kpis")]
[Authorize(Roles = RoleConstants.Admin)]
[EnableRateLimiting("admin")]
public class AdminKpiController(
    IAdminKpiService kpiService,
    ILogger<AdminKpiController> logger) : ControllerBase
{
    /// <summary>
    /// Get comprehensive dashboard KPI metrics for admin users.
    /// 
    /// METRICS INCLUDED:
    /// - User counts by role (User, ServiceProvider, Admin)
    /// - Service request statistics (total, open, accepted, completed, completion rate)
    /// - Bid statistics (total, pending, accepted, rejected, avg per request)
    /// - Authentication statistics (logins, failed attempts, expirations, active sessions)
    /// - Recent audit events summary (last 10 events)
    /// 
    /// SECURITY:
    /// - Admin-only endpoint ([Authorize(Roles = Admin)])
    /// - Returns 403 Forbidden for non-admin users
    /// - Uses efficient queries to minimize database load
    /// 
    /// USAGE:
    /// GET /api/admin/kpis
    /// </summary>
    /// <returns>Admin dashboard KPI metrics</returns>
    [HttpGet]
    [ProducesResponseType(typeof(AdminDashboardKpiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboardKpis()
    {
        logger.LogInformation("[AdminKpiController] Admin requesting dashboard KPIs");

        try
        {
            var kpis = await kpiService.GetDashboardKpisAsync();

            logger.LogInformation("[AdminKpiController] KPIs retrieved successfully");

            return Ok(kpis);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AdminKpiController] Error retrieving dashboard KPIs");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving dashboard metrics" });
        }
    }
}
