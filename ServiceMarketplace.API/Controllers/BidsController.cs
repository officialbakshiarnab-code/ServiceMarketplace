using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/bids")]
// Purpose: Bid operations for service providers and users.
// Roles: ServiceProvider (place bids), User (view bids for own request).
public class BidsController : ControllerBase
{
    private readonly IBidService _service;

    public BidsController(IBidService service)
    {
        _service = service;
    }

    // SERVICE PROVIDER places bid
    [Authorize(Roles = "ServiceProvider")]
    [HttpPost]
    public async Task<IActionResult> PlaceBid(CreateBidDto dto)
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(providerId))
            throw new UnauthorizedAccessException("Authentication is required.");

        await _service.PlaceBidAsync(dto, providerId);
        return Ok("Bid placed");
    }

    // SERVICE PROVIDER views their own bids
    [Authorize(Roles = "ServiceProvider")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(providerId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var bids = await _service.GetMyBidsAsync(providerId);
        return Ok(bids);
    }

    // USER views bids for their request
    [Authorize(Roles = "User")]
    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetBids(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var bids = await _service.GetBidsForRequestAsync(requestId, userId);
        return Ok(bids);
    }
}
