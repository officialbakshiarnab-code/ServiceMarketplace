using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.API.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize]
public class ProfilesController(IProfileDirectoryService service) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(typeof(ProfileSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ProfileSearchRequest request)
    {
        return Ok(await service.SearchAsync(request));
    }

    [HttpGet("{profileType}/{userId}")]
    [ProducesResponseType(typeof(ProfileDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetail(ContactProfileType profileType, string userId)
    {
        return Ok(await service.GetDetailAsync(profileType, userId));
    }
}
