using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public sealed class DeviceRegistrationsController(IDeviceRegistrationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceRegistrationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        return Ok(await service.GetMineAsync(GetUserId()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(DeviceRegistrationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(RegisterDeviceDto request)
    {
        return Ok(await service.RegisterAsync(GetUserId(), request));
    }

    [HttpDelete("{registrationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid registrationId)
    {
        await service.DeactivateAsync(registrationId, GetUserId());
        return NoContent();
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
