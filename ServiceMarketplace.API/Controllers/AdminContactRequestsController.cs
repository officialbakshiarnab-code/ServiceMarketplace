using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin/contact-requests")]
[Authorize(Policy = "AdminOnly")]
public class AdminContactRequestsController(IContactRequestService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ContactRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue([FromQuery] ContactRequestStatus? status)
    {
        return Ok(await service.GetAdminQueueAsync(status));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id)
    {
        return Ok(await service.GetAsync(id, GetUserId(), isAdmin: true));
    }

    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(typeof(ContactRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewContactRequestDto request)
    {
        return Ok(await service.ReviewAsync(id, GetUserId(), request));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
