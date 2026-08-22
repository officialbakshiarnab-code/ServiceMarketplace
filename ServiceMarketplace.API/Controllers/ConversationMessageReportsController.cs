using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/conversations/{conversationId:guid}/messages/{messageId:guid}/reports")]
[Authorize]
public sealed class ConversationMessageReportsController(IMessageModerationService moderationService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("messaging")]
    [ProducesResponseType(typeof(MessageReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Report(Guid conversationId, Guid messageId, CreateMessageReportDto request)
    {
        return Ok(await moderationService.ReportAsync(conversationId, messageId, GetUserId(), request));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
