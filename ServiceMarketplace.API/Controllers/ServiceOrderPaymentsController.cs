using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using System.Security.Claims;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/payment")]
[Authorize]
public class ServiceOrderPaymentsController(IServiceOrderPaymentService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ServiceOrderPaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get(Guid orderId)
    {
        var payment = await service.GetForOrderAsync(orderId, GetUserId());
        return payment == null ? NoContent() : Ok(payment);
    }

    [HttpPost]
    [Authorize(Policy = "UserOnly")]
    [ProducesResponseType(typeof(ServiceOrderPaymentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Record(Guid orderId, RecordServiceOrderPaymentDto dto)
    {
        return Ok(await service.RecordAsync(orderId, GetUserId(), dto));
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        return userId;
    }
}
