using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/product-catalog")]
public class ProductCatalogController(IProductCatalogService service) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        return Ok(await service.GetCategoriesAsync());
    }
}
