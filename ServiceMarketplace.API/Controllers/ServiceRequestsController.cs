using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/requests")]
// Purpose: Manage service requests for users and providers.
// Capabilities: service.customer (create/accept), service.provider (open/nearby list).
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    private readonly ILogger<ServiceRequestsController> _logger;

    public ServiceRequestsController(IServiceRequestService service, ILogger<ServiceRequestsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // USER creates a service request
    /// <summary>
    /// Creates a new service request.
    /// Rate Limited: 5 requests per 10 minutes per user.
    /// </summary>
    [Authorize(Policy = "UserOnly")]
    [HttpPost]
    [EnableRateLimiting("requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(CreateServiceRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[ServiceRequestsController] Create called but UserId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var userRole = User.FindFirstValue(ClaimTypes.Role);
        _logger.LogInformation("[ServiceRequestsController] User {UserId} creating request with role {Role}", userId, userRole);

        var id = await _service.CreateAsync(dto, userId);
        return Ok(new { RequestId = id });
    }

    // SERVICE PROVIDER views open requests
    [Authorize(Policy = "ProviderOnly")]
    [HttpGet("open")]
    public async Task<IActionResult> GetOpen()
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("[ServiceRequestsController] Provider {ProviderId} viewing open requests", providerId);

        if (string.IsNullOrWhiteSpace(providerId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetOpen called but ProviderId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var requests = await _service.GetOpenAsync(providerId);
        return Ok(requests);
    }

    // SERVICE PROVIDER searches nearby open requests
    [Authorize(Policy = "ProviderOnly")]
    [HttpPost("nearby")]
    public async Task<IActionResult> GetNearby(NearbySearchDto dto)
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("[ServiceRequestsController] Provider {ProviderId} searching nearby requests at {Lat},{Lng} radius {Radius}km",
            providerId, dto.Latitude, dto.Longitude, dto.RadiusKm);

        if (string.IsNullOrWhiteSpace(providerId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetNearby called but ProviderId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var requests = await _service.GetNearbyAsync(providerId, dto.Latitude, dto.Longitude, dto.RadiusKm);
        return Ok(requests);
    }

    // USER views their own requests
    [Authorize(Policy = "UserOnly")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetMine called but UserId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        _logger.LogInformation("[ServiceRequestsController] User {UserId} viewing own requests", userId);

        var requests = await _service.GetMyRequestsAsync(userId);
        return Ok(requests);
    }

    // SERVICE PROVIDER views available open requests (excluding own)
    [Authorize(Policy = "ProviderOnly")]
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable()
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(providerId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetAvailable called but ProviderId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        _logger.LogInformation("[ServiceRequestsController] Provider {ProviderId} viewing available requests", providerId);

        var requests = await _service.GetAvailableForProviderAsync(providerId);
        return Ok(requests);
    }

    // USER views details for their request
    [Authorize(Policy = "UserOnly")]
    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetById(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetById called but UserId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        _logger.LogInformation("[ServiceRequestsController] User {UserId} viewing request {RequestId}", userId, requestId);

        var request = await _service.GetByIdForUserAsync(requestId, userId);
        return Ok(request);
    }

    // SERVICE PROVIDER views details for any open request (to place bid)
    [Authorize(Policy = "ProviderOnly")]
    [HttpGet("{requestId}/details")]
    public async Task<IActionResult> GetRequestDetails(Guid requestId)
    {
        var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(providerId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetRequestDetails called but ProviderId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        _logger.LogInformation("[ServiceRequestsController] Provider {ProviderId} viewing request details {RequestId}", providerId, requestId);

        var request = await _service.GetByIdForProviderAsync(requestId, providerId);
        return Ok(request);
    }

    // USER accepts a bid
    [Authorize(Policy = "UserOnly")]
    [HttpPost("{requestId}/accept/{bidId}")]
    public async Task<IActionResult> AcceptBid(Guid requestId, Guid bidId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[ServiceRequestsController] AcceptBid called but UserId claim is missing");
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        _logger.LogInformation("[ServiceRequestsController] User {UserId} accepting bid {BidId} for request {RequestId}",
            userId, bidId, requestId);

        await _service.AcceptBidAsync(requestId, bidId, userId);
        return Ok("Bid accepted");
    }

    // USER views dashboard statistics
    /// <summary>
    /// Get dashboard statistics for the current user.
    /// Returns counts of open requests, active bids, and completed requests.
    /// Uses efficient single query to avoid N+1 problems.
    /// </summary>
    [Authorize(Policy = "UserOnly")]
    [HttpGet("stats")]
    [ProducesResponseType(typeof(UserDashboardStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDashboardStats()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[ServiceRequestsController] GetDashboardStats called but UserId claim is missing");
            return Unauthorized(new { error = "Authentication is required" });
        }

        _logger.LogInformation("[ServiceRequestsController] User {UserId} requesting dashboard stats", userId);

        var stats = await _service.GetDashboardStatsAsync(userId);
        return Ok(stats);
    }
}
