using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/product-delivery-orders")]
[Authorize]
public class ProductDeliveryOrdersController(
    IProductDeliveryOrderService service,
    ILogger<ProductDeliveryOrdersController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "ProductBuyerOnly")]
    [ProducesResponseType(typeof(ProductDeliveryOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateProductDeliveryOrderDto dto)
    {
        var buyerId = GetUserId();
        logger.LogInformation("[ProductDeliveryOrdersController] Buyer {BuyerId} creating product delivery order", buyerId);
        return Ok(await service.CreateAsync(buyerId, dto));
    }

    [HttpGet("mine/buyer")]
    [Authorize(Policy = "ProductBuyerOnly")]
    [ProducesResponseType(typeof(List<ProductDeliveryOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsBuyer()
    {
        var buyerId = GetUserId();
        return Ok(await service.GetMineAsBuyerAsync(buyerId));
    }

    [HttpGet("mine/seller")]
    [Authorize(Policy = "ProductSellerOnly")]
    [ProducesResponseType(typeof(List<ProductDeliveryOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsSeller()
    {
        var sellerId = GetUserId();
        return Ok(await service.GetMineAsSellerAsync(sellerId));
    }

    [HttpPost("{orderId:guid}/seller-status")]
    [Authorize(Policy = "ProductSellerOnly")]
    [ProducesResponseType(typeof(ProductDeliveryOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSellerStatus(Guid orderId, UpdateProductDeliveryStatusDto dto)
    {
        var sellerId = GetUserId();
        logger.LogInformation("[ProductDeliveryOrdersController] Seller {SellerId} updating product delivery order {OrderId}", sellerId, orderId);
        return Ok(await service.UpdateSellerStatusAsync(orderId, sellerId, dto));
    }

    [HttpPost("{orderId:guid}/buyer-cancel")]
    [Authorize(Policy = "ProductBuyerOnly")]
    [ProducesResponseType(typeof(ProductDeliveryOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsBuyer(Guid orderId, UpdateProductDeliveryStatusDto dto)
    {
        var buyerId = GetUserId();
        logger.LogInformation("[ProductDeliveryOrdersController] Buyer {BuyerId} cancelling product delivery order {OrderId}", buyerId, orderId);
        return Ok(await service.CancelAsBuyerAsync(orderId, buyerId, dto.CancellationReason));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
