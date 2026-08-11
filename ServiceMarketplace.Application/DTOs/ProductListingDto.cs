using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ProductListingDto
{
    public Guid Id { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? SellerBusinessName { get; set; }
    public Guid ProductCategoryId { get; set; }
    public string ProductCategoryName { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public string? ServiceZoneName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public ProductCondition Condition { get; set; }
    public string ConditionLabel { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public string? ConditionNotes { get; set; }
    public string? InspectionChecklist { get; set; }
    public int? PurchaseYear { get; set; }
    public bool HasOriginalBill { get; set; }
    public bool HasWarranty { get; set; }
    public List<string> UsedInspectionPrompts { get; set; } = [];
    public ProductListingStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
