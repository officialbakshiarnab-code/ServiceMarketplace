using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/provider-applications")]
[Authorize]
public class ProviderApplicationsController(IProviderApplicationService service) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(ProviderApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();
        var application = await service.GetMyApplicationAsync(userId);
        return application == null ? NoContent() : Ok(application);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ProviderApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertMine(UpsertProviderApplicationDto dto)
    {
        var application = await service.UpsertMyApplicationAsync(GetUserId(), dto);
        return Ok(application);
    }

    [HttpPost("me/submit")]
    [ProducesResponseType(typeof(ProviderApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitMine()
    {
        var application = await service.SubmitMyApplicationAsync(GetUserId());
        return Ok(application);
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
