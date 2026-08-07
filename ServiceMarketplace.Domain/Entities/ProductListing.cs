using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ProductListing : BaseAuditableEntity
{
    public Guid SellerId { get; set; }
    public SellerProfile SellerProfile { get; set; } = null!;

    public Guid ProductCategoryId { get; set; }
    public ProductCategory ProductCategory { get; set; } = null!;

    public Guid? ServiceZoneId { get; set; }
    public ServiceZone? ServiceZone { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public ProductCondition Condition { get; set; } = ProductCondition.New;
    public string? ConditionNotes { get; set; }
    public string? InspectionChecklist { get; set; }
    public int? PurchaseYear { get; set; }
    public bool HasOriginalBill { get; set; }
    public bool HasWarranty { get; set; }
    public ProductListingStatus Status { get; set; } = ProductListingStatus.Draft;
}
