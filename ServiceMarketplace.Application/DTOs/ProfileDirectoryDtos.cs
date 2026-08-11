using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ProfileSearchRequest
{
    public string? Query { get; set; }
    public ContactProfileType? ProfileType { get; set; }
    public Guid? ServiceCategoryId { get; set; }
    public Guid? ZoneId { get; set; }
    public int Take { get; set; } = 25;
}

public class ProfileSearchResponse
{
    public int TotalCount { get; set; }
    public List<ProfileSearchItemDto> Items { get; set; } = [];
}

public class ProfileSearchItemDto
{
    public ContactProfileType ProfileType { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Summary { get; set; }
    public Guid? ServiceCategoryId { get; set; }
    public string? ServiceCategoryName { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public decimal? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsAvailable { get; set; }
    public int ActiveOfferCount { get; set; }
}

public class ProfileDetailDto : ProfileSearchItemDto
{
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Highlights { get; set; } = [];
}
