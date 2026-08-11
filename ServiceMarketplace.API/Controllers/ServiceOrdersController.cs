using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class ServiceOrdersController(IServiceOrderService service, ILogger<ServiceOrdersController> logger) : ControllerBase
{
    [HttpGet("customer")]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(List<ServiceOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerOrders()
    {
        var userId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] Customer {UserId} listing service orders", userId);
        return Ok(await service.GetForCustomerAsync(userId));
    }

    [HttpGet("provider")]
    [Authorize(Policy = "ProviderOnly")]
    [ProducesResponseType(typeof(List<ServiceOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviderOrders()
    {
        var providerId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] Provider {ProviderId} listing service orders", providerId);
        return Ok(await service.GetForProviderAsync(providerId));
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(ServiceOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid orderId)
    {
        var userId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] User {UserId} viewing service order {OrderId}", userId, orderId);
        return Ok(await service.GetByIdAsync(orderId, userId));
    }

    [HttpPost("{orderId:guid}/start")]
    [Authorize(Policy = "ProviderOnly")]
    [ProducesResponseType(typeof(ServiceOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(Guid orderId)
    {
        var providerId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] Provider {ProviderId} starting service order {OrderId}", providerId, orderId);
        return Ok(await service.StartAsync(orderId, providerId));
    }

    [HttpPost("{orderId:guid}/provider-complete")]
    [Authorize(Policy = "ProviderOnly")]
    [ProducesResponseType(typeof(ServiceOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProviderComplete(Guid orderId)
    {
        var providerId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] Provider {ProviderId} marking service order {OrderId} complete", providerId, orderId);
        return Ok(await service.MarkProviderCompletedAsync(orderId, providerId));
    }

    [HttpPost("{orderId:guid}/confirm-complete")]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(ServiceOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmComplete(Guid orderId)
    {
        var customerId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] Customer {CustomerId} confirming service order {OrderId} complete", customerId, orderId);
        return Ok(await service.ConfirmCustomerCompletionAsync(orderId, customerId));
    }

    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType(typeof(ServiceOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid orderId, CancelServiceOrderDto dto)
    {
        var userId = GetUserId();
        logger.LogInformation("[ServiceOrdersController] User {UserId} cancelling service order {OrderId}", userId, orderId);
        return Ok(await service.CancelAsync(orderId, userId, dto));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
