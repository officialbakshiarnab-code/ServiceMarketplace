using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Controllers;

/// <summary>
/// Admin-only controller for viewing audit logs.
/// Provides secure access to authentication and authorization audit trail.
/// Rate Limited: 200 requests per minute per admin user.
/// </summary>
[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Policy = "BothRoleOnly")]
[EnableRateLimiting("admin")]
public class AdminAuditLogsController(
    IAuditLogService auditLogService,
    ILogger<AdminAuditLogsController> logger) : ControllerBase
{
    /// <summary>
    /// Query audit logs with filtering and pagination.
    /// 
    /// SECURITY:
    /// - Admin-only endpoint ([Authorize(Roles = Admin)])
    /// - Returns complete audit trail for compliance
    /// - Supports filtering by user, event type, date range
    /// - Paginated results (max 100 per page)
    /// 
    /// USAGE:
    /// GET /api/admin/audit-logs?page=1&pageSize=20
    /// GET /api/admin/audit-logs?userId={id}&eventType=Login
    /// GET /api/admin/audit-logs?startDate=2025-01-01&endDate=2025-01-31
    /// </summary>
    /// <param name="request">Query parameters</param>
    /// <returns>Paginated audit log results</returns>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryAuditLogs([FromQuery] AuditLogQueryRequest request)
    {
        logger.LogInformation("[AdminAuditLogsController] Admin querying audit logs: Page={Page}, PageSize={PageSize}, UserId={UserId}, EventType={EventType}",
            request.Page, request.PageSize, request.UserId ?? "(all)", request.EventType ?? "(all)");

        try
        {
            var response = await auditLogService.QueryAuditLogsAsync(request);

            logger.LogInformation("[AdminAuditLogsController] Query returned {Count} results out of {Total} total",
                response.Items.Count, response.TotalCount);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AdminAuditLogsController] Error querying audit logs");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while querying audit logs" });
        }
    }

    /// <summary>
    /// Get available event types for filtering.
    /// </summary>
    [HttpGet("event-types")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public IActionResult GetEventTypes()
    {
        var eventTypes = new[]
        {
            "Registration",
            "Login",
            "Logout",
            "SessionExpired"
        };

        return Ok(eventTypes);
    }

    /// <summary>
    /// Get available roles for filtering.
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public IActionResult GetRoles()
    {
        return Ok(RoleConstants.AllRoles);
    }
}
