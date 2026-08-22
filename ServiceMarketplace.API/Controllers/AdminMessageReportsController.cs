using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin/message-reports")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("admin")]
public sealed class AdminMessageReportsController(IMessageModerationService moderationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<MessageReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue([FromQuery] MessageReportStatus? status)
    {
        return Ok(await moderationService.GetAdminQueueAsync(status));
    }

    [HttpPost("{reportId:guid}/review")]
    [ProducesResponseType(typeof(MessageReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(Guid reportId, ReviewMessageReportDto request)
    {
        return Ok(await moderationService.ReviewAsync(reportId, GetAdminId(), request));
    }

    private string GetAdminId()
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        return adminId;
    }
}
