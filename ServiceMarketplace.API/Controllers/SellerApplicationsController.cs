using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/seller-applications")]
[Authorize]
public class SellerApplicationsController(ISellerApplicationService service) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(SellerApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMine()
    {
        var application = await service.GetMyApplicationAsync(GetUserId());
        return application == null ? NoContent() : Ok(application);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(SellerApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertMine(UpsertSellerApplicationDto dto)
    {
        return Ok(await service.UpsertMyApplicationAsync(GetUserId(), dto));
    }

    [HttpPost("me/submit")]
    [ProducesResponseType(typeof(SellerApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitMine()
    {
        return Ok(await service.SubmitMyApplicationAsync(GetUserId()));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
