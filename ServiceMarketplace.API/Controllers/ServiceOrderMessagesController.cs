using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/messages")]
[Authorize]
public class ServiceOrderMessagesController(IServiceOrderCommunicationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceOrderMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(Guid orderId)
    {
        return Ok(await service.GetMessagesAsync(orderId, GetUserId()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServiceOrderMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessage(Guid orderId, CreateServiceOrderMessageDto dto)
    {
        return Ok(await service.SendMessageAsync(orderId, GetUserId(), dto));
    }

    [HttpPost("read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkThreadRead(Guid orderId)
    {
        var count = await service.MarkThreadReadAsync(orderId, GetUserId());
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
