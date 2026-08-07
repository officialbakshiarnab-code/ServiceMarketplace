using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/marketplace/search")]
[Authorize(Policy = "UserOnly")]
public class MarketplaceSearchController(IMarketplaceSearchService service, ILogger<MarketplaceSearchController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MarketplaceSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] MarketplaceSearchItemType? type = null,
        [FromQuery] Guid? serviceCategoryId = null,
        [FromQuery] Guid? productCategoryId = null,
        [FromQuery] Guid? zoneId = null,
        [FromQuery] ProductCondition? productCondition = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] MarketplaceSearchSort sort = MarketplaceSearchSort.Relevance,
        [FromQuery] int take = 30)
    {
        logger.LogInformation("[MarketplaceSearchController] Searching marketplace for {Query}", q);

        return Ok(await service.SearchAsync(new MarketplaceSearchRequest
        {
            Query = q,
            Type = type,
            ServiceCategoryId = serviceCategoryId,
            ProductCategoryId = productCategoryId,
            ZoneId = zoneId,
            ProductCondition = productCondition,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Sort = sort,
            Take = take
        }));
    }
}
