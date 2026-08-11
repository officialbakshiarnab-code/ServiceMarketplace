namespace ServiceMarketplace.Domain.Entities;

public class ProductInspectionPrompt : BaseAuditableEntity
{
    public Guid ProductCategoryId { get; set; }
    public ProductCategory ProductCategory { get; set; } = null!;

    public string Prompt { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
