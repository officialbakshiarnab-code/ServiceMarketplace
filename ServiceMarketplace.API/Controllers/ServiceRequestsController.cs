using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/requests")]
// Purpose: Manage service requests for users and providers.
// Roles: User (create/accept), ServiceProvider (open/nearby list).
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;

    public ServiceRequestsController(IServiceRequestService service)
    {
        _service = service;
    }

    // USER creates a service request
    [Authorize(Roles = "User")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var id = await _service.CreateAsync(dto, userId);
        return Ok(new { RequestId = id });
    }

    // SERVICE PROVIDER views open requests
    [Authorize(Roles = "ServiceProvider")]
    [HttpGet("open")]
    public async Task<IActionResult> GetOpen()
    {
        var requests = await _service.GetOpenAsync();
        return Ok(requests);
    }

    // SERVICE PROVIDER searches nearby open requests
    [Authorize(Roles = "ServiceProvider")]
    [HttpPost("nearby")]
    public async Task<IActionResult> GetNearby(NearbySearchDto dto)
    {
        var requests = await _service.GetNearbyAsync(dto.Latitude, dto.Longitude, dto.RadiusKm);
        return Ok(requests);
    }

    // USER accepts a bid
    [Authorize(Roles = "User")]
    [HttpPost("{requestId}/accept/{bidId}")]
    public async Task<IActionResult> AcceptBid(Guid requestId, Guid bidId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _service.AcceptBidAsync(requestId, bidId, userId);
        return Ok("Bid accepted");
    }
}
