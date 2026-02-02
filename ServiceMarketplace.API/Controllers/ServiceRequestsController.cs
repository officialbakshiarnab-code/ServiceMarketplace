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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

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

    // USER views their own requests
    [Authorize(Roles = "User")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var requests = await _service.GetMyRequestsAsync(userId);
        return Ok(requests);
    }

    // SERVICE PROVIDER views available open requests (excluding own)
    [Authorize(Roles = "ServiceProvider")]
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable()
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(providerId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var requests = await _service.GetAvailableForProviderAsync(providerId);
        return Ok(requests);
    }

    // USER views details for their request
    [Authorize(Roles = "User")]
    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetById(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var request = await _service.GetByIdForUserAsync(requestId, userId);
        return Ok(request);
    }

    // SERVICE PROVIDER views details for any open request (to place bid)
    [Authorize(Roles = "ServiceProvider")]
    [HttpGet("{requestId}/details")]
    public async Task<IActionResult> GetRequestDetails(Guid requestId)
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(providerId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var request = await _service.GetByIdForProviderAsync(requestId, providerId);
        return Ok(request);
    }

    // USER accepts a bid
    [Authorize(Roles = "User")]
    [HttpPost("{requestId}/accept/{bidId}")]
    public async Task<IActionResult> AcceptBid(Guid requestId, Guid bidId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        await _service.AcceptBidAsync(requestId, bidId, userId);
        return Ok("Bid accepted");
    }
}
