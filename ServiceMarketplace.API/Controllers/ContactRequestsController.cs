using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/contact-requests")]
[Authorize]
public class ContactRequestsController(IContactRequestService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ContactRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateContactRequestDto request)
    {
        return Ok(await service.CreateAsync(GetUserId(), request));
    }

    [HttpGet("sent")]
    [ProducesResponseType(typeof(List<ContactRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSent()
    {
        return Ok(await service.GetSentAsync(GetUserId()));
    }

    [HttpGet("received")]
    [ProducesResponseType(typeof(List<ContactRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceived()
    {
        return Ok(await service.GetReceivedAsync(GetUserId()));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ContactRequestSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        return Ok(await service.GetSummaryAsync(GetUserId()));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id)
    {
        return Ok(await service.GetAsync(id, GetUserId()));
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ContactRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        return Ok(await service.CancelAsync(id, GetUserId()));
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(ContactRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id)
    {
        return Ok(await service.CompleteAsync(id, GetUserId()));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
