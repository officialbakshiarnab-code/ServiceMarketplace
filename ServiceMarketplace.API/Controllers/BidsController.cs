using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/bids")]
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
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _service.PlaceBidAsync(dto, providerId);
        return Ok("Bid placed");
    }

    // USER views bids for their request
    [Authorize(Roles = "User")]
    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetBids(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bids = await _service.GetBidsForRequestAsync(requestId, userId);
        return Ok(bids);
    }
}
