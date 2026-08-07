namespace ServiceMarketplace.Application.DTOs;

public class ProductCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public List<string> UsedInspectionPrompts { get; set; } = [];
}
