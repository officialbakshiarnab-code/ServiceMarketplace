using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin/seller-applications")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("admin")]
public class AdminSellerApplicationsController(ISellerApplicationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SellerApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplications([FromQuery] SellerApplicationStatus? status = null)
    {
        return Ok(await service.GetForAdminAsync(status));
    }

    [HttpPost("{applicationId:guid}/review")]
    [ProducesResponseType(typeof(SellerApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(Guid applicationId, ReviewSellerApplicationDto dto)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        return Ok(await service.ReviewAsync(applicationId, adminId, dto));
    }
}
