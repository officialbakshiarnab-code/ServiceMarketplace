using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin/order-reviews")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("admin")]
public class AdminServiceOrderReviewsController(IServiceOrderReviewService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceOrderReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] bool includeHidden = true)
    {
        return Ok(await service.GetForAdminAsync(includeHidden));
    }

    [HttpPost("{reviewId:guid}/moderate")]
    [ProducesResponseType(typeof(ServiceOrderReviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Moderate(Guid reviewId, ModerateServiceOrderReviewDto dto)
    {
        return Ok(await service.ModerateAsync(reviewId, GetAdminId(), dto));
    }

    private string GetAdminId()
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        return adminId;
    }
}
