using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/service-catalog")]
public class ServiceCatalogController(IServiceCatalogService service) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        return Ok(await service.GetCategoriesAsync());
    }

    [HttpGet("zones")]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceZoneDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones()
    {
        return Ok(await service.GetZonesAsync());
    }
}
