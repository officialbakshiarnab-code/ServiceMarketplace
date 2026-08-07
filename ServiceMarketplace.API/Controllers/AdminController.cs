using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/admin")]
[EnableRateLimiting("admin")]
public class AdminController(
    AppDbContext dbContext,
    IProviderApplicationService providerApplicationService,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpPut("approve-kyc/{userId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ApproveKyc([FromRoute] string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            logger.LogWarning("[AdminController] ApproveKyc called with invalid userId {UserId}", userId);
            return BadRequest(new { message = "A valid userId is required" });
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userGuid);
        if (user == null)
        {
            logger.LogWarning("[AdminController] ApproveKyc: user not found: {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        if (user.UserType != ServiceMarketplace.Domain.Enums.UserType.Provider)
        {
            logger.LogWarning("[AdminController] ApproveKyc: user {UserId} is not a provider", userId);
            return BadRequest(new { message = "User is not a provider." });
        }

        if (!user.IsKycSubmitted)
            return BadRequest(new { message = "KYC not submitted." });

        if (user.IsKycApproved)
            return BadRequest(new { message = "Already approved." });

        var adminId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(adminId))
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to determine admin user id." });

        var providerProfile = await dbContext.ServiceProviderProfiles.FirstOrDefaultAsync(p => p.UserId == userGuid);
        if (providerProfile != null)
        {
            await providerApplicationService.ReviewAsync(providerProfile.Id, adminId, new ReviewProviderApplicationDto
            {
                Status = ProviderApplicationStatus.Approved,
                ReviewNotes = "Approved through legacy KYC approval endpoint."
            });

            logger.LogInformation("[AdminController] Provider application approved for user {UserId} by admin {AdminId}", userId, adminId);
            return Ok(new { message = "KYC approved successfully." });
        }

        user.IsKycApproved = true;
        user.KycApprovedByUserId = adminId;
        user.KycApprovedOn = DateTime.UtcNow;
        user.IsActive = true;
        user.UpdatedDate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        logger.LogInformation("[AdminController] KYC approved for user {UserId} by admin {AdminId}", userId, adminId);
        return Ok(new { message = "KYC approved successfully." });
    }
}
