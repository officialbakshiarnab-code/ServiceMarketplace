using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin/provider-applications")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("admin")]
public class AdminProviderApplicationsController(IProviderApplicationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplications([FromQuery] ProviderApplicationStatus? status = null)
    {
        var applications = await service.GetForAdminAsync(status);
        return Ok(applications);
    }

    [HttpPost("{applicationId:guid}/review")]
    [ProducesResponseType(typeof(ProviderApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(Guid applicationId, ReviewProviderApplicationDto dto)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        var application = await service.ReviewAsync(applicationId, adminId, dto);
        return Ok(application);
    }
}
