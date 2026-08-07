using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin/marketplace-economics")]
[Authorize(Policy = "AdminOnly")]
public class AdminMarketplaceEconomicsController(IMarketplaceEconomicsService service) : ControllerBase
{
    [HttpPost("platform-payment-intents/{intentId:guid}/verify")]
    [ProducesResponseType(typeof(ServiceOrderPaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyPlatformPaymentIntent(Guid intentId, VerifyPlatformPaymentIntentDto dto)
    {
        return Ok(await service.VerifyPlatformIntentAsync(intentId, GetUserId(), dto));
    }

    [HttpGet("payouts")]
    [ProducesResponseType(typeof(List<ProviderPayoutDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayouts([FromQuery] ProviderPayoutStatus? status = null)
    {
        return Ok(await service.GetPayoutsAsync(status));
    }

    [HttpPost("payouts/{payoutId:guid}/mark-paid")]
    [ProducesResponseType(typeof(ProviderPayoutDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkPayoutPaid(Guid payoutId, MarkProviderPayoutPaidDto dto)
    {
        return Ok(await service.MarkPayoutPaidAsync(payoutId, GetUserId(), dto));
    }

    [HttpGet("disputes")]
    [ProducesResponseType(typeof(List<ServiceOrderDisputeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisputes([FromQuery] ServiceOrderDisputeStatus? status = null)
    {
        return Ok(await service.GetDisputesAsync(status));
    }

    [HttpPost("disputes/{disputeId:guid}/resolve")]
    [ProducesResponseType(typeof(ServiceOrderDisputeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveDispute(Guid disputeId, ResolveServiceOrderDisputeDto dto)
    {
        return Ok(await service.ResolveDisputeAsync(disputeId, GetUserId(), dto));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
