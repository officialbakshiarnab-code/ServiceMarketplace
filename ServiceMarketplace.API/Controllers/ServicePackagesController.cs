using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/service-packages")]
public class ServicePackagesController(IServicePackageService service, ILogger<ServicePackagesController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(List<ServicePackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive([FromQuery] Guid? categoryId = null, [FromQuery] Guid? zoneId = null)
    {
        logger.LogInformation("[ServicePackagesController] Listing active service packages");
        return Ok(await service.GetActiveAsync(categoryId, zoneId));
    }

    [HttpGet("mine")]
    [Authorize(Policy = "ProviderOnly")]
    [ProducesResponseType(typeof(List<ServicePackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var providerId = GetUserId();
        logger.LogInformation("[ServicePackagesController] Provider {ProviderId} listing own service packages", providerId);
        return Ok(await service.GetMineAsync(providerId));
    }

    [HttpPost]
    [Authorize(Policy = "ProviderOnly")]
    [ProducesResponseType(typeof(ServicePackageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(UpsertServicePackageDto dto)
    {
        var providerId = GetUserId();
        logger.LogInformation("[ServicePackagesController] Provider {ProviderId} creating service package", providerId);
        return Ok(await service.CreateAsync(providerId, dto));
    }

    [HttpPut("{packageId:guid}")]
    [Authorize(Policy = "ProviderOnly")]
    [ProducesResponseType(typeof(ServicePackageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid packageId, UpsertServicePackageDto dto)
    {
        var providerId = GetUserId();
        logger.LogInformation("[ServicePackagesController] Provider {ProviderId} updating service package {PackageId}", providerId, packageId);
        return Ok(await service.UpdateAsync(packageId, providerId, dto));
    }

    [HttpPost("{packageId:guid}/book")]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(ServiceOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Book(Guid packageId, BookServicePackageDto dto)
    {
        var customerId = GetUserId();
        logger.LogInformation("[ServicePackagesController] Customer {CustomerId} booking service package {PackageId}", customerId, packageId);
        return Ok(await service.BookAsync(packageId, customerId, dto));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
