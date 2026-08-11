namespace ServiceMarketplace.Domain.Entities;

public class ProductCategory : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<ProductListing> ProductListings { get; set; } = new List<ProductListing>();
    public ICollection<ProductInspectionPrompt> InspectionPrompts { get; set; } = new List<ProductInspectionPrompt>();
}
