using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationInboxService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<UserNotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false)
    {
        return Ok(await service.GetMineAsync(GetUserId(), unreadOnly));
    }

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(typeof(UserNotificationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        return Ok(await service.MarkReadAsync(notificationId, GetUserId()));
    }

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead()
    {
        var count = await service.MarkAllReadAsync(GetUserId());
        return Ok(new { count });
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
