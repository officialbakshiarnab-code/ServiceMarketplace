using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Authorize]
public class ServiceOrderReviewsController(IServiceOrderReviewService service) : ControllerBase
{
    [HttpGet("api/orders/{orderId:guid}/review")]
    [ProducesResponseType(typeof(ServiceOrderReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetForOrder(Guid orderId)
    {
        var review = await service.GetForOrderAsync(orderId, GetUserId());
        return review == null ? NoContent() : Ok(review);
    }

    [HttpPost("api/orders/{orderId:guid}/review")]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(ServiceOrderReviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid orderId, CreateServiceOrderReviewDto dto)
    {
        return Ok(await service.CreateAsync(orderId, GetUserId(), dto));
    }

    [HttpGet("api/providers/{providerId}/reviews")]
    [ProducesResponseType(typeof(List<ServiceOrderReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForProvider(string providerId)
    {
        return Ok(await service.GetForProviderAsync(providerId));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
