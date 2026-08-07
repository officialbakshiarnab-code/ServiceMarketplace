using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}")]
[Authorize]
public class ServiceOrderEconomicsController(IMarketplaceEconomicsService service) : ControllerBase
{
    [HttpGet("platform-payment-intent")]
    [ProducesResponseType(typeof(PlatformPaymentIntentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetPlatformPaymentIntent(Guid orderId)
    {
        var intent = await service.GetPlatformIntentForOrderAsync(orderId, GetUserId());
        return intent == null ? NoContent() : Ok(intent);
    }

    [HttpPost("platform-payment-intent")]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(PlatformPaymentIntentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePlatformPaymentIntent(Guid orderId)
    {
        return Ok(await service.CreatePlatformIntentAsync(orderId, GetUserId()));
    }

    [HttpGet("payout")]
    [ProducesResponseType(typeof(ProviderPayoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetPayout(Guid orderId)
    {
        var payout = await service.GetPayoutForOrderAsync(orderId, GetUserId());
        return payout == null ? NoContent() : Ok(payout);
    }

    [HttpGet("disputes")]
    [ProducesResponseType(typeof(List<ServiceOrderDisputeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisputes(Guid orderId)
    {
        return Ok(await service.GetDisputesForOrderAsync(orderId, GetUserId()));
    }

    [HttpPost("disputes")]
    [ProducesResponseType(typeof(ServiceOrderDisputeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateDispute(Guid orderId, CreateServiceOrderDisputeDto dto)
    {
        return Ok(await service.CreateDisputeAsync(orderId, GetUserId(), dto));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
