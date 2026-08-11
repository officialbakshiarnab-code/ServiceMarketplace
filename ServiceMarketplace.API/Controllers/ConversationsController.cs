using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ConversationInboxItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInbox([FromQuery] bool includeArchived = false, [FromQuery] int take = 50)
    {
        return Ok(await conversationService.GetInboxAsync(GetUserId(), includeArchived, take));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ConversationUnreadCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        return Ok(new ConversationUnreadCountDto { Count = await conversationService.GetUnreadCountAsync(GetUserId()) });
    }

    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(typeof(ConversationDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid conversationId)
    {
        return Ok(await conversationService.GetAsync(conversationId, GetUserId()));
    }

    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ConversationMessagesPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] DateTime? before = null, [FromQuery] int take = 50)
    {
        return Ok(await conversationService.GetMessagesAsync(conversationId, GetUserId(), before, take));
    }

    [HttpPost("{conversationId:guid}/messages")]
    [EnableRateLimiting("messaging")]
    [ProducesResponseType(typeof(ConversationMessageDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> SendMessage(Guid conversationId, SendConversationMessageDto request)
    {
        var message = await conversationService.SendMessageAsync(conversationId, GetUserId(), request);
        return CreatedAtAction(nameof(GetMessages), new { conversationId }, message);
    }

    [HttpPost("{conversationId:guid}/read")]
    [ProducesResponseType(typeof(ConversationReadResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid conversationId)
    {
        return Ok(await conversationService.MarkReadAsync(conversationId, GetUserId()));
    }

    [HttpPatch("{conversationId:guid}/preferences")]
    [ProducesResponseType(typeof(ConversationDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferences(Guid conversationId, UpdateConversationPreferencesDto request)
    {
        return Ok(await conversationService.UpdatePreferencesAsync(conversationId, GetUserId(), request));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
