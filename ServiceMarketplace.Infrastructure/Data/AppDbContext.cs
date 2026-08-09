using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using UserRoleEntity = ServiceMarketplace.Domain.Entities.UserRole;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>
/// Application DbContext for marketplace persistence using custom auth tables.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<ServiceProviderProfile> ServiceProviderProfiles => Set<ServiceProviderProfile>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ServiceZone> ServiceZones => Set<ServiceZone>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderMessage> ServiceOrderMessages => Set<ServiceOrderMessage>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<ServiceOrderPayment> ServiceOrderPayments => Set<ServiceOrderPayment>();
    public DbSet<PlatformPaymentIntent> PlatformPaymentIntents => Set<PlatformPaymentIntent>();
    public DbSet<ProviderPayout> ProviderPayouts => Set<ProviderPayout>();
    public DbSet<ServiceOrderDispute> ServiceOrderDisputes => Set<ServiceOrderDispute>();
    public DbSet<ServiceOrderReview> ServiceOrderReviews => Set<ServiceOrderReview>();
    public DbSet<ServiceOrderAuditEvent> ServiceOrderAuditEvents => Set<ServiceOrderAuditEvent>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductInspectionPrompt> ProductInspectionPrompts => Set<ProductInspectionPrompt>();
    public DbSet<ProductListing> ProductListings => Set<ProductListing>();
    public DbSet<ProductDeliveryOrder> ProductDeliveryOrders => Set<ProductDeliveryOrder>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("ServiceRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.ServiceCategoryId);
            entity.Property(e => e.Location).IsRequired();
            entity.Property(e => e.ServiceZoneId);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Urgency)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(ServiceRequestUrgency.Flexible);
            entity.Property(e => e.Requirements).HasMaxLength(2000);

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("IX_ServiceRequests_CustomerId");

            entity.HasIndex(e => e.ServiceCategoryId)
                .HasDatabaseName("IX_ServiceRequests_ServiceCategoryId");

            entity.HasIndex(e => e.ServiceZoneId)
                .HasDatabaseName("IX_ServiceRequests_ServiceZoneId");

            entity.HasOne(e => e.ServiceCategory)
                .WithMany(c => c.ServiceRequests)
                .HasForeignKey(e => e.ServiceCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ServiceZone)
                .WithMany(z => z.ServiceRequests)
                .HasForeignKey(e => e.ServiceZoneId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Bid>(entity =>
        {
            entity.ToTable("Bids");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ServiceProviderId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.ProposedDateTime).IsRequired();
            entity.Property(e => e.EstimatedDurationMinutes);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.ServiceRequestId)
                .HasDatabaseName("IX_Bids_ServiceRequestId");

            entity.HasIndex(e => e.ServiceProviderId)
                .HasDatabaseName("IX_Bids_ServiceProviderId");

            entity.HasOne(e => e.ServiceRequest)
                .WithMany(r => r.Bids)
                .HasForeignKey(e => e.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ServiceOrder>(entity =>
        {
            entity.ToTable("ServiceOrders");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.ProviderId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.AgreedAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.ScheduledStartAt).IsRequired();
            entity.Property(e => e.EstimatedDurationMinutes);
            entity.Property(e => e.Status)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(ServiceOrderStatus.PendingStart);
            entity.Property(e => e.CancelledByUserId).HasMaxLength(450);
            entity.Property(e => e.CancellationReason).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceRequest)
                .WithOne(r => r.ServiceOrder)
                .HasForeignKey<ServiceOrder>(e => e.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AcceptedBid)
                .WithOne(b => b.ServiceOrder)
                .HasForeignKey<ServiceOrder>(e => e.AcceptedBidId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ServicePackage)
                .WithMany(p => p.ServiceOrders)
                .HasForeignKey(e => e.ServicePackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ServiceRequestId)
                .IsUnique()
                .HasDatabaseName("UX_ServiceOrders_ServiceRequestId");

            entity.HasIndex(e => e.AcceptedBidId)
                .IsUnique()
                .HasDatabaseName("UX_ServiceOrders_AcceptedBidId");

            entity.HasIndex(e => e.CustomerId)
                .HasDatabaseName("IX_ServiceOrders_CustomerId");

            entity.HasIndex(e => e.ProviderId)
                .HasDatabaseName("IX_ServiceOrders_ProviderId");

            entity.HasIndex(e => e.ServicePackageId)
                .HasDatabaseName("IX_ServiceOrders_ServicePackageId");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_ServiceOrders_Status");
        });

        builder.Entity<ServicePackage>(entity =>
        {
            entity.ToTable("ServicePackages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProviderId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceCategory)
                .WithMany(c => c.ServicePackages)
                .HasForeignKey(e => e.ServiceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ServiceZone)
                .WithMany(z => z.ServicePackages)
                .HasForeignKey(e => e.ServiceZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.IsActive, e.ServiceCategoryId, e.ServiceZoneId })
                .HasDatabaseName("IX_ServicePackages_ActiveCategoryZone");

            entity.HasIndex(e => e.ProviderId)
                .HasDatabaseName("IX_ServicePackages_ProviderId");
        });

        builder.Entity<ServiceOrderMessage>(entity =>
        {
            entity.ToTable("ServiceOrderMessages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SenderUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.RecipientUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Body)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrder)
                .WithMany(o => o.Messages)
                .HasForeignKey(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ServiceOrderId, e.CreatedAt })
                .HasDatabaseName("IX_ServiceOrderMessages_OrderCreated");

            entity.HasIndex(e => e.RecipientUserId)
                .HasDatabaseName("IX_ServiceOrderMessages_RecipientUserId");
        });

        builder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("UserNotifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Type)
                .HasConversion<short>()
                .IsRequired();

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.IsRead)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt })
                .HasDatabaseName("IX_UserNotifications_UserReadCreated");

            entity.HasIndex(e => e.ServiceOrderId)
                .HasDatabaseName("IX_UserNotifications_ServiceOrderId");

            entity.HasIndex(e => e.ProductDeliveryOrderId)
                .HasDatabaseName("IX_UserNotifications_ProductDeliveryOrderId");

            entity.HasIndex(e => e.ProviderPayoutId)
                .HasDatabaseName("IX_UserNotifications_ProviderPayoutId");

            entity.HasIndex(e => e.ServiceOrderDisputeId)
                .HasDatabaseName("IX_UserNotifications_ServiceOrderDisputeId");

            entity.HasIndex(e => e.ContactRequestId)
                .HasDatabaseName("IX_UserNotifications_ContactRequestId");
        });

        builder.Entity<ContactRequest>(entity =>
        {
            entity.ToTable("ContactRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RequesterUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.TargetUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.TargetProfileType)
                .HasConversion<short>()
                .IsRequired();

            entity.Property(e => e.Kind)
                .HasConversion<short>()
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(ContactRequestStatus.PendingAdminReview);

            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.CompletedByUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.RequesterUserId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_ContactRequests_RequesterStatusCreated");

            entity.HasIndex(e => new { e.TargetUserId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_ContactRequests_TargetStatusCreated");

            entity.HasIndex(e => new { e.Kind, e.Status })
                .HasDatabaseName("IX_ContactRequests_KindStatus");
        });

        builder.Entity<SellerProfile>(entity =>
        {
            entity.ToTable("SellerProfiles");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.StoreName).IsRequired().HasMaxLength(150);
            entity.Property(s => s.BusinessName).HasMaxLength(150);
            entity.Property(s => s.Description).HasMaxLength(1000);
            entity.Property(s => s.Gstin).HasMaxLength(30);
            entity.Property(s => s.PickupAddress).IsRequired().HasMaxLength(500);
            entity.Property(s => s.City).IsRequired().HasMaxLength(100);
            entity.Property(s => s.State).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Status).HasConversion<short>().IsRequired();
            entity.Property(s => s.ReviewedByUserId).HasMaxLength(450);
            entity.Property(s => s.ReviewNotes).HasMaxLength(1000);
            entity.Property(s => s.RejectionReason).HasMaxLength(1000);
            entity.Property(s => s.CreatedAt).IsRequired();

            entity.HasOne(s => s.User)
                .WithOne(u => u.SellerProfile)
                .HasForeignKey<SellerProfile>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.ServiceZone)
                .WithMany()
                .HasForeignKey(s => s.ServiceZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(s => s.UserId)
                .IsUnique()
                .HasDatabaseName("UX_SellerProfiles_UserId");

            entity.HasIndex(s => s.Status)
                .HasDatabaseName("IX_SellerProfiles_Status");

            entity.HasIndex(s => new { s.State, s.City })
                .HasDatabaseName("IX_SellerProfiles_Area");
        });

        builder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Slug).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(c => c.SortOrder).IsRequired().HasDefaultValue(0);
            entity.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("UX_ProductCategories_Slug");
            entity.HasIndex(c => new { c.IsActive, c.SortOrder }).HasDatabaseName("IX_ProductCategories_ActiveSort");

            entity.HasData(
                new ProductCategory
                {
                    Id = new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
                    Name = "Home Essentials",
                    Slug = "home-essentials",
                    Description = "Everyday household products for local buyers",
                    IsActive = true,
                    SortOrder = 10,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ProductCategory
                {
                    Id = new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
                    Name = "Tools And Hardware",
                    Slug = "tools-and-hardware",
                    Description = "Tools, fittings, and basic hardware supplies",
                    IsActive = true,
                    SortOrder = 20,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ProductCategory
                {
                    Id = new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc3"),
                    Name = "Electronics",
                    Slug = "electronics",
                    Description = "Small electronics and accessories",
                    IsActive = true,
                    SortOrder = 30,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        builder.Entity<ProductInspectionPrompt>(entity =>
        {
            entity.ToTable("ProductInspectionPrompts");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Prompt).IsRequired().HasMaxLength(300);
            entity.Property(p => p.SortOrder).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasOne(p => p.ProductCategory)
                .WithMany(c => c.InspectionPrompts)
                .HasForeignKey(p => p.ProductCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.ProductCategoryId, p.IsActive, p.SortOrder })
                .HasDatabaseName("IX_ProductInspectionPrompts_CategoryActiveSort");

            entity.HasData(
                new ProductInspectionPrompt
                {
                    Id = new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd1"),
                    ProductCategoryId = new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
                    Prompt = "Check visible wear, stains, cracks, missing parts, and whether the product has been cleaned before pickup.",
                    SortOrder = 10,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ProductInspectionPrompt
                {
                    Id = new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd2"),
                    ProductCategoryId = new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
                    Prompt = "Check rust, grip condition, moving parts, safety guards, serial/model labels, and included accessories.",
                    SortOrder = 10,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ProductInspectionPrompt
                {
                    Id = new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd3"),
                    ProductCategoryId = new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc3"),
                    Prompt = "Power on the device, check battery/charging, ports, display, buttons, invoice/warranty status, and reset/lock status.",
                    SortOrder = 10,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        builder.Entity<ProductListing>(entity =>
        {
            entity.ToTable("ProductListings");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.SellerId).IsRequired();
            entity.Property(p => p.Title).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(1000);
            entity.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
            entity.Property(p => p.StockQuantity).IsRequired();
            entity.Property(p => p.ImageUrl).HasMaxLength(500);
            entity.Property(p => p.Condition)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(ProductCondition.New);
            entity.Property(p => p.ConditionNotes).HasMaxLength(1000);
            entity.Property(p => p.InspectionChecklist).HasMaxLength(2000);
            entity.Property(p => p.PurchaseYear);
            entity.Property(p => p.HasOriginalBill).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.HasWarranty).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.Status).HasConversion<short>().IsRequired();
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasOne(p => p.SellerProfile)
                .WithMany(s => s.ProductListings)
                .HasPrincipalKey(s => s.UserId)
                .HasForeignKey(p => p.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.ProductCategory)
                .WithMany(c => c.ProductListings)
                .HasForeignKey(p => p.ProductCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.ServiceZone)
                .WithMany(z => z.ProductListings)
                .HasForeignKey(p => p.ServiceZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => new { p.Status, p.ProductCategoryId, p.ServiceZoneId })
                .HasDatabaseName("IX_ProductListings_StatusCategoryZone");

            entity.HasIndex(p => new { p.Status, p.Condition, p.ProductCategoryId, p.ServiceZoneId })
                .HasDatabaseName("IX_ProductListings_StatusConditionCategoryZone");

            entity.HasIndex(p => p.SellerId)
                .HasDatabaseName("IX_ProductListings_SellerId");
        });

        builder.Entity<ProductDeliveryOrder>(entity =>
        {
            entity.ToTable("ProductDeliveryOrders");
            entity.HasKey(o => o.Id);

            entity.Property(o => o.BuyerId).IsRequired().HasMaxLength(450);
            entity.Property(o => o.Quantity).IsRequired();
            entity.Property(o => o.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(o => o.TotalPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(o => o.Status)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(ProductDeliveryStatus.PendingSellerConfirmation);
            entity.Property(o => o.DeliveryRecipientName).IsRequired().HasMaxLength(150);
            entity.Property(o => o.DeliveryPhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(o => o.DeliveryAddress).IsRequired().HasMaxLength(500);
            entity.Property(o => o.DeliveryCity).IsRequired().HasMaxLength(100);
            entity.Property(o => o.DeliveryState).IsRequired().HasMaxLength(100);
            entity.Property(o => o.BuyerNotes).HasMaxLength(1000);
            entity.Property(o => o.CancellationReason).HasMaxLength(1000);
            entity.Property(o => o.CancelledByUserId).HasMaxLength(450);
            entity.Property(o => o.CreatedAt).IsRequired();

            entity.HasOne(o => o.ProductListing)
                .WithMany(p => p.DeliveryOrders)
                .HasForeignKey(o => o.ProductListingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.SellerProfile)
                .WithMany(s => s.ProductDeliveryOrders)
                .HasPrincipalKey(s => s.UserId)
                .HasForeignKey(o => o.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.ServiceZone)
                .WithMany(z => z.ProductDeliveryOrders)
                .HasForeignKey(o => o.ServiceZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(o => o.ProductListingId)
                .HasDatabaseName("IX_ProductDeliveryOrders_ProductListingId");

            entity.HasIndex(o => new { o.BuyerId, o.Status, o.CreatedAt })
                .HasDatabaseName("IX_ProductDeliveryOrders_BuyerStatusCreated");

            entity.HasIndex(o => new { o.SellerId, o.Status, o.CreatedAt })
                .HasDatabaseName("IX_ProductDeliveryOrders_SellerStatusCreated");

            entity.HasIndex(o => o.ServiceZoneId)
                .HasDatabaseName("IX_ProductDeliveryOrders_ServiceZoneId");
        });

        builder.Entity<ServiceOrderPayment>(entity =>
        {
            entity.ToTable("ServiceOrderPayments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.PlatformFeeAmount).HasPrecision(18, 2);
            entity.Property(e => e.ProviderPayoutAmount).HasPrecision(18, 2);
            entity.Property(e => e.Method).HasConversion<short>().IsRequired();
            entity.Property(e => e.Status).HasConversion<short>().IsRequired();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RecordedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.RecordedAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrder)
                .WithOne(o => o.Payment)
                .HasForeignKey<ServiceOrderPayment>(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PlatformPaymentIntent)
                .WithMany()
                .HasForeignKey(e => e.PlatformPaymentIntentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ServiceOrderId)
                .IsUnique()
                .HasDatabaseName("UX_ServiceOrderPayments_ServiceOrderId");

            entity.HasIndex(e => e.PlatformPaymentIntentId)
                .IsUnique()
                .HasDatabaseName("UX_ServiceOrderPayments_PlatformPaymentIntentId");

            entity.HasIndex(e => new { e.ProviderId, e.Status })
                .HasDatabaseName("IX_ServiceOrderPayments_ProviderStatus");
        });

        builder.Entity<PlatformPaymentIntent>(entity =>
        {
            entity.ToTable("PlatformPaymentIntents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.PlatformFeeAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.ProviderPayoutAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Status).HasConversion<short>().IsRequired();
            entity.Property(e => e.GatewayReference).IsRequired().HasMaxLength(100);
            entity.Property(e => e.GatewayPaymentId).HasMaxLength(200);
            entity.Property(e => e.VerificationNotes).HasMaxLength(1000);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.VerifiedByUserId).HasMaxLength(450);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrder)
                .WithMany(o => o.PlatformPaymentIntents)
                .HasForeignKey(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.GatewayReference)
                .IsUnique()
                .HasDatabaseName("UX_PlatformPaymentIntents_GatewayReference");

            entity.HasIndex(e => new { e.ServiceOrderId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_PlatformPaymentIntents_OrderStatusCreated");

            entity.HasIndex(e => new { e.CustomerId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_PlatformPaymentIntents_CustomerStatusCreated");
        });

        builder.Entity<ProviderPayout>(entity =>
        {
            entity.ToTable("ProviderPayouts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.GrossAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.PlatformFeeAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.PayoutAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Status).HasConversion<short>().IsRequired();
            entity.Property(e => e.PayoutReference).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.MarkedPaidByUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrderPayment)
                .WithOne(p => p.ProviderPayout)
                .HasForeignKey<ProviderPayout>(e => e.ServiceOrderPaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ServiceOrder)
                .WithMany(o => o.ProviderPayouts)
                .HasForeignKey(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ServiceOrderPaymentId)
                .IsUnique()
                .HasDatabaseName("UX_ProviderPayouts_ServiceOrderPaymentId");

            entity.HasIndex(e => new { e.ProviderId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_ProviderPayouts_ProviderStatusCreated");

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("IX_ProviderPayouts_StatusCreated");
        });

        builder.Entity<ServiceOrderDispute>(entity =>
        {
            entity.ToTable("ServiceOrderDisputes");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RaisedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.AgainstUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<short>().IsRequired();
            entity.Property(e => e.ResolutionNotes).HasMaxLength(1000);
            entity.Property(e => e.ResolvedByUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrder)
                .WithMany(o => o.Disputes)
                .HasForeignKey(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ServiceOrderPayment)
                .WithMany(p => p.Disputes)
                .HasForeignKey(e => e.ServiceOrderPaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.ServiceOrderId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_ServiceOrderDisputes_OrderStatusCreated");

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("IX_ServiceOrderDisputes_StatusCreated");
        });

        builder.Entity<ServiceOrderReview>(entity =>
        {
            entity.ToTable("ServiceOrderReviews");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.Feedback).HasMaxLength(2000);
            entity.Property(e => e.IsHidden).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.ModerationNotes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrder)
                .WithOne(o => o.Review)
                .HasForeignKey<ServiceOrderReview>(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ServiceOrderId)
                .IsUnique()
                .HasDatabaseName("UX_ServiceOrderReviews_ServiceOrderId");

            entity.HasIndex(e => new { e.ProviderId, e.IsHidden, e.CreatedAt })
                .HasDatabaseName("IX_ServiceOrderReviews_ProviderVisibleCreated");
        });

        builder.Entity<ServiceOrderAuditEvent>(entity =>
        {
            entity.ToTable("ServiceOrderAuditEvents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ActorUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ActorRole).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FromStatus).HasMaxLength(100);
            entity.Property(e => e.ToStatus).HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.ServiceOrder)
                .WithMany(o => o.AuditEvents)
                .HasForeignKey(e => e.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ServiceOrderId, e.CreatedAt })
                .HasDatabaseName("IX_ServiceOrderAuditEvents_OrderCreated");

            entity.HasIndex(e => e.ActorUserId)
                .HasDatabaseName("IX_ServiceOrderAuditEvents_ActorUserId");
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.HasIndex(e => e.SessionId)
                .HasDatabaseName("IX_AuditLogs_SessionId");

            entity.HasIndex(e => e.TimestampUtc)
                .IsDescending()
                .HasDatabaseName("IX_AuditLogs_TimestampUtc");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_AuditLogs_UserId");
        });

        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.SessionId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TokenHash)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.TokenFamily)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.RevocationReason).HasMaxLength(100);
            entity.Property(e => e.IssuedFromIpAddress).HasMaxLength(45);
            entity.Property(e => e.IssuedFromUserAgent).HasMaxLength(500);
            entity.Property(e => e.LastUsedIpAddress).HasMaxLength(45);

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

            entity.HasIndex(e => e.TokenFamily)
                .HasDatabaseName("IX_RefreshTokens_TokenFamily");

            entity.HasIndex(e => e.TokenHash)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_TokenHash");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");
        });

        builder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).HasMaxLength(150);
            entity.Property(u => u.NormalizedEmail).HasMaxLength(150);
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.Property(u => u.NormalizedPhoneNumber).HasMaxLength(20);
            entity.Property(u => u.SecondaryPhoneNumber).HasMaxLength(20);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.IsEmailVerified).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.IsPhoneVerified).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.TwoFactorEnabled).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(u => u.UserType)
                .HasConversion<short>()
                .IsRequired()
                .HasDefaultValue(UserType.Customer);
            entity.Property(u => u.GovIdFilePath).HasMaxLength(500);
            entity.Property(u => u.KycApprovedByUserId).HasMaxLength(450);
            entity.Property(u => u.CreatedDate).IsRequired().HasDefaultValueSql("now()");

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("UX_Users_NormalizedEmail");

            entity.HasIndex(u => u.NormalizedPhoneNumber)
                .IsUnique()
                .HasDatabaseName("UX_Users_NormalizedPhoneNumber");

            entity.HasIndex(u => u.UserType)
                .HasDatabaseName("IX_Users_UserType");
        });

        builder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Description).HasMaxLength(250);
            entity.Property(r => r.CreatedDate).IsRequired().HasDefaultValueSql("now()");
            entity.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UX_Roles_Name");

            entity.HasData(
                new Role
                {
                    Id = new Guid("11111111-1111-1111-1111-111111111111"),
                    Name = "User",
                    Description = "Customer account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    Id = new Guid("22222222-2222-2222-2222-222222222222"),
                    Name = "ServiceProvider",
                    Description = "Service provider account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    Id = new Guid("33333333-3333-3333-3333-333333333333"),
                    Name = "Both",
                    Description = "Dual customer and service provider account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    Id = new Guid("44444444-4444-4444-4444-444444444444"),
                    Name = "Admin",
                    Description = "Administrator account role",
                    CreatedDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        builder.Entity<UserRoleEntity>(entity =>
        {
            entity.ToTable("UserRoles");

            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(ur => ur.RoleId)
                .HasDatabaseName("IX_UserRoles_RoleId");
        });

        builder.Entity<ServiceProviderProfile>(entity =>
        {
            entity.ToTable("ServiceProviderProfiles");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.DisplayName).IsRequired().HasMaxLength(150);
            entity.Property(p => p.BusinessName).HasMaxLength(150);
            entity.Property(p => p.Bio).HasMaxLength(1000);
            entity.Property(p => p.Skills).IsRequired().HasMaxLength(1000);
            entity.Property(p => p.PrimaryCategory).IsRequired().HasMaxLength(100);
            entity.Property(p => p.ServiceCategoryId);
            entity.Property(p => p.ServiceAreaCity).IsRequired().HasMaxLength(100);
            entity.Property(p => p.ServiceAreaState).IsRequired().HasMaxLength(100);
            entity.Property(p => p.ServiceAreaZone).HasMaxLength(100);
            entity.Property(p => p.ServiceZoneId);
            entity.Property(p => p.HourlyRate).HasPrecision(18, 2);
            entity.Property(p => p.Status).HasConversion<short>().IsRequired();
            entity.Property(p => p.ReviewedByUserId).HasMaxLength(450);
            entity.Property(p => p.ReviewNotes).HasMaxLength(1000);
            entity.Property(p => p.RejectionReason).HasMaxLength(1000);
            entity.Property(p => p.AverageRating).HasPrecision(3, 2).IsRequired().HasDefaultValue(0m);
            entity.Property(p => p.ReviewCount).IsRequired().HasDefaultValue(0);

            entity.HasOne(p => p.User)
                .WithOne(u => u.ServiceProviderProfile)
                .HasForeignKey<ServiceProviderProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.ServiceCategory)
                .WithMany()
                .HasForeignKey(p => p.ServiceCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(p => p.ServiceZone)
                .WithMany()
                .HasForeignKey(p => p.ServiceZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.UserId)
                .IsUnique()
                .HasDatabaseName("UX_ServiceProviderProfiles_UserId");

            entity.HasIndex(p => p.Status)
                .HasDatabaseName("IX_ServiceProviderProfiles_Status");

            entity.HasIndex(p => new { p.ServiceAreaState, p.ServiceAreaCity })
                .HasDatabaseName("IX_ServiceProviderProfiles_ServiceArea");

            entity.HasIndex(p => p.ServiceCategoryId)
                .HasDatabaseName("IX_ServiceProviderProfiles_ServiceCategoryId");

            entity.HasIndex(p => p.ServiceZoneId)
                .HasDatabaseName("IX_ServiceProviderProfiles_ServiceZoneId");
        });

        builder.Entity<ServiceCategory>(entity =>
        {
            entity.ToTable("ServiceCategories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Slug).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(c => c.SortOrder).IsRequired().HasDefaultValue(0);
            entity.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("UX_ServiceCategories_Slug");
            entity.HasIndex(c => new { c.IsActive, c.SortOrder }).HasDatabaseName("IX_ServiceCategories_ActiveSort");

            entity.HasData(
                new ServiceCategory
                {
                    Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                    Name = "Plumbing",
                    Slug = "plumbing",
                    Description = "Leaks, taps, pipes, fittings, and water-flow issues",
                    IsActive = true,
                    SortOrder = 10,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ServiceCategory
                {
                    Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                    Name = "Electrical",
                    Slug = "electrical",
                    Description = "Wiring, fixtures, switchboards, fans, and basic electrical repairs",
                    IsActive = true,
                    SortOrder = 20,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ServiceCategory
                {
                    Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                    Name = "Cleaning",
                    Slug = "cleaning",
                    Description = "Home and small-office cleaning services",
                    IsActive = true,
                    SortOrder = 30,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        builder.Entity<ServiceZone>(entity =>
        {
            entity.ToTable("ServiceZones");
            entity.HasKey(z => z.Id);
            entity.Property(z => z.Country).IsRequired().HasMaxLength(100);
            entity.Property(z => z.State).IsRequired().HasMaxLength(100);
            entity.Property(z => z.City).IsRequired().HasMaxLength(100);
            entity.Property(z => z.ZoneName).HasMaxLength(100);
            entity.Property(z => z.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(z => z.PinCodeRegion).HasMaxLength(50);
            entity.Property(z => z.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(z => z.SortOrder).IsRequired().HasDefaultValue(0);
            entity.HasIndex(z => new { z.State, z.City, z.ZoneName }).HasDatabaseName("IX_ServiceZones_Area");
            entity.HasIndex(z => new { z.IsActive, z.SortOrder }).HasDatabaseName("IX_ServiceZones_ActiveSort");

            entity.HasData(
                new ServiceZone
                {
                    Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
                    Country = "India",
                    State = "West Bengal",
                    City = "Kolkata",
                    ZoneName = "Central Kolkata",
                    DisplayName = "Central Kolkata, Kolkata, West Bengal",
                    PinCodeRegion = "7000xx",
                    IsActive = true,
                    SortOrder = 10,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ServiceZone
                {
                    Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
                    Country = "India",
                    State = "West Bengal",
                    City = "Kolkata",
                    ZoneName = "South Kolkata",
                    DisplayName = "South Kolkata, Kolkata, West Bengal",
                    PinCodeRegion = "7000xx",
                    IsActive = true,
                    SortOrder = 20,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                },
                new ServiceZone
                {
                    Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"),
                    Country = "India",
                    State = "West Bengal",
                    City = "Kolkata",
                    ZoneName = "North Kolkata",
                    DisplayName = "North Kolkata, Kolkata, West Bengal",
                    PinCodeRegion = "7000xx",
                    IsActive = true,
                    SortOrder = 30,
                    CreatedAt = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc)
                });
        });
    }
}
