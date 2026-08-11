using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class Phase12ProductDeliveryTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid HomeEssentialsCategoryId = new("cccccccc-cccc-cccc-cccc-ccccccccccc1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase12ProductDeliveryTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BuyerCanCreateProductDeliveryOrder_AndOrderAppearsForBuyerAndSeller()
    {
        await SeedProductCatalogAsync();
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase12-seller{Guid.NewGuid():N}@test.com");
        var listing = await CreateProductAsync(sellerToken, stockQuantity: 5);

        var buyerToken = await RegisterAndLoginAsync($"phase12-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);

        var createResponse = await _client.PostAsJsonAsync("/api/product-delivery-orders", BuildCreateOrder(listing.Id, quantity: 2));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await ReadJsonAsync<ProductDeliveryOrderDto>(createResponse);
        created.ProductListingId.Should().Be(listing.Id);
        created.Quantity.Should().Be(2);
        created.TotalPrice.Should().Be(listing.Price * 2);
        created.Status.Should().Be(ProductDeliveryStatus.PendingSellerConfirmation);
        created.SellerPickupAddress.Should().Contain("Phase 12 Seller Lane");

        await using (var dbContext = await _factory.GetDbContextAsync())
        {
            var persistedListing = await dbContext.ProductListings.AsNoTracking().SingleAsync(p => p.Id == listing.Id);
            persistedListing.StockQuantity.Should().Be(3);

            var notifications = await dbContext.UserNotifications.AsNoTracking()
                .Where(n => n.ProductDeliveryOrderId == created.Id)
                .ToListAsync();
            notifications.Should().HaveCount(2);
        }

        var buyerOrdersResponse = await _client.GetAsync("/api/product-delivery-orders/mine/buyer");
        buyerOrdersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var buyerOrders = await ReadJsonAsync<List<ProductDeliveryOrderDto>>(buyerOrdersResponse);
        buyerOrders.Should().ContainSingle(o => o.Id == created.Id);

        SetAuthHeader(sellerToken);
        var sellerOrdersResponse = await _client.GetAsync("/api/product-delivery-orders/mine/seller");
        sellerOrdersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sellerOrders = await ReadJsonAsync<List<ProductDeliveryOrderDto>>(sellerOrdersResponse);
        sellerOrders.Should().ContainSingle(o => o.Id == created.Id);
    }

    [Fact]
    public async Task SellerCanAdvanceProductDeliveryOrderThroughFulfillmentLifecycle()
    {
        await SeedProductCatalogAsync();
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase12-lifecycle-seller{Guid.NewGuid():N}@test.com");
        var listing = await CreateProductAsync(sellerToken, stockQuantity: 2);
        var order = await CreateOrderAsBuyerAsync(listing.Id, quantity: 1);

        SetAuthHeader(sellerToken);

        order = await MoveSellerStatusAsync(order.Id, ProductDeliveryStatus.Confirmed);
        order.Status.Should().Be(ProductDeliveryStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();

        order = await MoveSellerStatusAsync(order.Id, ProductDeliveryStatus.ReadyForPickup);
        order.Status.Should().Be(ProductDeliveryStatus.ReadyForPickup);
        order.ReadyForPickupAt.Should().NotBeNull();

        order = await MoveSellerStatusAsync(order.Id, ProductDeliveryStatus.OutForDelivery);
        order.Status.Should().Be(ProductDeliveryStatus.OutForDelivery);
        order.OutForDeliveryAt.Should().NotBeNull();

        order = await MoveSellerStatusAsync(order.Id, ProductDeliveryStatus.Delivered);
        order.Status.Should().Be(ProductDeliveryStatus.Delivered);
        order.DeliveredAt.Should().NotBeNull();

        var invalidResponse = await _client.PostAsJsonAsync($"/api/product-delivery-orders/{order.Id}/seller-status", new UpdateProductDeliveryStatusDto
        {
            Status = ProductDeliveryStatus.Cancelled,
            CancellationReason = "Too late."
        });
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProductDeliveryValidation_BlocksOwnProductAndOversell_AndBuyerCancelRestoresStock()
    {
        await SeedProductCatalogAsync();
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase12-validation-seller{Guid.NewGuid():N}@test.com");
        var listing = await CreateProductAsync(sellerToken, stockQuantity: 2);

        SetAuthHeader(sellerToken);
        var ownProductResponse = await _client.PostAsJsonAsync("/api/product-delivery-orders", BuildCreateOrder(listing.Id, quantity: 1));
        ownProductResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var buyerToken = await RegisterAndLoginAsync($"phase12-validation-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);
        var oversellResponse = await _client.PostAsJsonAsync("/api/product-delivery-orders", BuildCreateOrder(listing.Id, quantity: 3));
        oversellResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var createResponse = await _client.PostAsJsonAsync("/api/product-delivery-orders", BuildCreateOrder(listing.Id, quantity: 2));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadJsonAsync<ProductDeliveryOrderDto>(createResponse);

        var cancelResponse = await _client.PostAsJsonAsync($"/api/product-delivery-orders/{created.Id}/buyer-cancel", new UpdateProductDeliveryStatusDto
        {
            CancellationReason = "Buyer changed schedule."
        });
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await ReadJsonAsync<ProductDeliveryOrderDto>(cancelResponse);
        cancelled.Status.Should().Be(ProductDeliveryStatus.Cancelled);

        await using var dbContext = await _factory.GetDbContextAsync();
        var persistedListing = await dbContext.ProductListings.AsNoTracking().SingleAsync(p => p.Id == listing.Id);
        persistedListing.StockQuantity.Should().Be(2);
    }

    private async Task<ProductDeliveryOrderDto> CreateOrderAsBuyerAsync(Guid listingId, int quantity)
    {
        var buyerToken = await RegisterAndLoginAsync($"phase12-order-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);

        var response = await _client.PostAsJsonAsync("/api/product-delivery-orders", BuildCreateOrder(listingId, quantity));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync<ProductDeliveryOrderDto>(response);
    }

    private async Task<ProductDeliveryOrderDto> MoveSellerStatusAsync(Guid orderId, ProductDeliveryStatus status)
    {
        var response = await _client.PostAsJsonAsync($"/api/product-delivery-orders/{orderId}/seller-status", new UpdateProductDeliveryStatusDto
        {
            Status = status
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync<ProductDeliveryOrderDto>(response);
    }

    private async Task<ProductListingDto> CreateProductAsync(string sellerToken, int stockQuantity)
    {
        SetAuthHeader(sellerToken);
        var response = await _client.PostAsJsonAsync("/api/products", new UpsertProductListingDto
        {
            ProductCategoryId = HomeEssentialsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = $"Phase 12 product {Guid.NewGuid():N}",
            Description = "Product available for delivery lifecycle testing.",
            Price = 499,
            StockQuantity = stockQuantity,
            ImageUrl = "https://example.test/phase12-product.jpg",
            Condition = ProductCondition.New,
            IsActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync<ProductListingDto>(response);
    }

    private static CreateProductDeliveryOrderDto BuildCreateOrder(Guid listingId, int quantity)
    {
        return new CreateProductDeliveryOrderDto
        {
            ProductListingId = listingId,
            Quantity = quantity,
            DeliveryRecipientName = "Phase 12 Buyer",
            DeliveryPhoneNumber = "9876543210",
            DeliveryAddress = "22 Buyer Road, Kolkata",
            DeliveryCity = "Kolkata",
            DeliveryState = "West Bengal",
            ServiceZoneId = SouthKolkataZoneId,
            BuyerNotes = "Deliver after 6 PM."
        };
    }

    private async Task<string> CreateApprovedSellerTokenAsync(string email)
    {
        var sellerToken = await RegisterAndLoginAsync(email, RoleConstants.User);
        SetAuthHeader(sellerToken);

        var upsertResponse = await _client.PutAsJsonAsync("/api/seller-applications/me", new UpsertSellerApplicationDto
        {
            StoreName = "Phase 12 Delivery Store",
            BusinessName = "Phase 12 Commerce",
            Description = "Seller for delivery lifecycle tests.",
            PickupAddress = "Phase 12 Seller Lane, Kolkata, West Bengal",
            City = "Kolkata",
            State = "West Bengal",
            ServiceZoneId = SouthKolkataZoneId,
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = true,
            BusinessVerificationSubmitted = true
        });
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await _client.PostAsync("/api/seller-applications/me/submit", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await ReadJsonAsync<SellerApplicationDto>(submitResponse);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var approveResponse = await _client.PostAsJsonAsync($"/api/admin/seller-applications/{submitted.Id}/review", new ReviewSellerApplicationDto
        {
            Status = SellerApplicationStatus.Approved,
            ReviewNotes = "Approved for Phase 12 delivery test."
        });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return await LoginAsync(email);
    }

    private async Task RegisterAsync(string email, string role)
    {
        var registerReq = AuthRequestBuilder.CreateDefault()
            .WithEmail(email)
            .WithRole(role)
            .BuildRegisterRequest();

        var response = await _client.PostAsJsonAsync("/api/auth/register", registerReq);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> RegisterAndLoginAsync(string email, string role)
    {
        await RegisterAsync(email, role);
        return await LoginAsync(email);
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JwtTestHelper.ExtractToken(response.Content) ?? throw new InvalidOperationException("No token returned");
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        var adminEmail = $"phase12-admin{Guid.NewGuid():N}@test.com";
        await RegisterAsync(adminEmail, RoleConstants.User);

        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = adminEmail.Trim().ToUpperInvariant();
        var user = await dbContext.Users.FirstAsync(u => u.NormalizedEmail == normalizedEmail);
        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == RoleConstants.Admin);

        dbContext.UserRoles.Add(new ServiceMarketplace.Domain.Entities.UserRole
        {
            UserId = user.Id,
            RoleId = adminRole.Id
        });
        user.UserType = UserType.Admin;
        user.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return await LoginAsync(adminEmail);
    }

    private async Task SeedProductCatalogAsync()
    {
        await using var dbContext = await _factory.GetDbContextAsync();

        if (!await dbContext.ServiceZones.AnyAsync(z => z.Id == SouthKolkataZoneId))
        {
            dbContext.ServiceZones.Add(new ServiceZone
            {
                Id = SouthKolkataZoneId,
                Country = "India",
                State = "West Bengal",
                City = "Kolkata",
                ZoneName = "South Kolkata",
                DisplayName = "South Kolkata, Kolkata, West Bengal",
                PinCodeRegion = "7000xx",
                IsActive = true,
                SortOrder = 20,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await dbContext.ProductCategories.AnyAsync(c => c.Id == HomeEssentialsCategoryId))
        {
            dbContext.ProductCategories.Add(new ProductCategory
            {
                Id = HomeEssentialsCategoryId,
                Name = "Home Essentials",
                Slug = "home-essentials",
                Description = "Everyday household products for local buyers",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private void SetAuthHeader(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Response body could not be deserialized.");
    }
}
