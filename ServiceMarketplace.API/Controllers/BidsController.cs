using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.Constants;
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
    private readonly ILogger<BidsController> _logger;

    public BidsController(IBidService service, ILogger<BidsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // SERVICE PROVIDER places bid
    /// <summary>
    /// Places a bid on a service request.
    /// Rate Limited: 10 bids per 5 minutes per user.
    /// </summary>
    [Authorize(Roles = RoleConstants.ServiceProvider)]
    [HttpPost]
    [EnableRateLimiting("bids")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> PlaceBid(CreateBidDto dto)
    {
        try
        {
            var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.LogWarning("[BidsController] PlaceBid called but ProviderId claim is missing");
                return Unauthorized(new { error = "Authentication is required" });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            _logger.LogInformation("[BidsController] Provider {ProviderId} placing bid on request {RequestId} with role {Role}",
                providerId, dto.ServiceRequestId, userRole);

            await _service.PlaceBidAsync(dto, providerId);
            return Ok(new { message = "Bid placed successfully" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "[BidsController] PlaceBid: Invalid argument");
            return BadRequest(new { error = "Invalid bid data provided" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[BidsController] PlaceBid: Invalid operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BidsController] PlaceBid: Unexpected error");
            return BadRequest(new { error = "An error occurred while placing the bid" });
        }
    }

    // SERVICE PROVIDER views their own bids
    [Authorize(Roles = RoleConstants.ServiceProvider)]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        try
        {
            var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.LogWarning("[BidsController] GetMine called but ProviderId claim is missing");
                return Unauthorized(new { error = "Authentication is required" });
            }

            _logger.LogInformation("[BidsController] Provider {ProviderId} viewing own bids", providerId);

            var bids = await _service.GetMyBidsAsync(providerId);
            return Ok(bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BidsController] GetMine: Unexpected error");
            return BadRequest(new { error = "An error occurred while retrieving bids" });
        }
    }

    // USER views bids for their request
    [Authorize(Roles = RoleConstants.User)]
    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetBids(Guid requestId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("[BidsController] GetBids called but UserId claim is missing");
                return Unauthorized(new { error = "Authentication is required" });
            }

            _logger.LogInformation("[BidsController] User {UserId} viewing bids for request {RequestId}", userId, requestId);

            var bids = await _service.GetBidsForRequestAsync(requestId, userId);
            return Ok(bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BidsController] GetBids: Unexpected error");
            return BadRequest(new { error = "An error occurred while retrieving bids" });
        }
    }
}

