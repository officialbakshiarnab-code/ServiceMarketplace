using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductListingsController(IProductListingService service, ILogger<ProductListingsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProductBuyerOnly")]
    [ProducesResponseType(typeof(List<ProductListingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive([FromQuery] Guid? categoryId = null, [FromQuery] Guid? zoneId = null, [FromQuery] ProductCondition? condition = null)
    {
        logger.LogInformation("[ProductListingsController] Listing active products");
        return Ok(await service.GetActiveAsync(categoryId, zoneId, condition));
    }

    [HttpGet("mine")]
    [Authorize(Policy = "ProductSellerOnly")]
    [ProducesResponseType(typeof(List<ProductListingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var sellerId = GetUserId();
        logger.LogInformation("[ProductListingsController] Seller {SellerId} listing own products", sellerId);
        return Ok(await service.GetMineAsync(sellerId));
    }

    [HttpPost]
    [Authorize(Policy = "ProductSellerOnly")]
    [ProducesResponseType(typeof(ProductListingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(UpsertProductListingDto dto)
    {
        var sellerId = GetUserId();
        logger.LogInformation("[ProductListingsController] Seller {SellerId} creating product listing", sellerId);
        return Ok(await service.CreateAsync(sellerId, dto));
    }

    [HttpPut("{listingId:guid}")]
    [Authorize(Policy = "ProductSellerOnly")]
    [ProducesResponseType(typeof(ProductListingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid listingId, UpsertProductListingDto dto)
    {
        var sellerId = GetUserId();
        logger.LogInformation("[ProductListingsController] Seller {SellerId} updating product listing {ListingId}", sellerId, listingId);
        return Ok(await service.UpdateAsync(listingId, sellerId, dto));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
