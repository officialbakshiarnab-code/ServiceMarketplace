using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/audit")]
[Authorize]
public class ServiceOrderAuditController(IServiceOrderAuditService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceOrderAuditEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid orderId)
    {
        return Ok(await service.GetForOrderAsync(orderId, GetUserId()));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
