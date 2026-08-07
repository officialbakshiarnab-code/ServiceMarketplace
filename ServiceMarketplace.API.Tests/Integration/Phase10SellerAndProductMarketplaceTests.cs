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

public class Phase10SellerAndProductMarketplaceTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid HomeEssentialsCategoryId = new("cccccccc-cccc-cccc-cccc-ccccccccccc1");
    private static readonly Guid ToolsCategoryId = new("cccccccc-cccc-cccc-cccc-ccccccccccc2");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase10SellerAndProductMarketplaceTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProductCatalog_ReturnsSeededProductCategories()
    {
        await SeedProductCategoriesAsync();

        var response = await _client.GetAsync("/api/product-catalog/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await ReadJsonAsync<List<ProductCategoryDto>>(response);
        categories.Should().Contain(c => c.Id == HomeEssentialsCategoryId && c.Name == "Home Essentials");
        categories.Should().Contain(c => c.Id == ToolsCategoryId && c.Name == "Tools And Hardware");
    }

    [Fact]
    public async Task ApprovedSeller_CanCreateProduct_AndBuyerCanBrowseActiveListings()
    {
        var sellerEmail = $"phase10-seller{Guid.NewGuid():N}@test.com";
        await SeedProductCategoriesAsync();

        var sellerToken = await RegisterAndLoginAsync(sellerEmail, RoleConstants.User);
        JwtTestHelper.GetClaimValues(sellerToken, MarketplaceCapabilityConstants.ClaimType)
            .Should().Contain(MarketplaceCapabilityConstants.ProductBuyer);
        JwtTestHelper.GetClaimValues(sellerToken, MarketplaceCapabilityConstants.ClaimType)
            .Should().NotContain(MarketplaceCapabilityConstants.ProductSeller);

        SetAuthHeader(sellerToken);
        var blockedCreate = await _client.PostAsJsonAsync("/api/products", new UpsertProductListingDto
        {
            ProductCategoryId = HomeEssentialsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = "Blocked product",
            Description = "Seller is not approved yet.",
            Price = 250,
            StockQuantity = 2,
            IsActive = true
        });
        blockedCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var upsertResponse = await _client.PutAsJsonAsync("/api/seller-applications/me", new UpsertSellerApplicationDto
        {
            StoreName = "Phase 10 Store",
            BusinessName = "Phase 10 Commerce",
            Description = "Local pilot product seller.",
            PickupAddress = "10 Seller Lane, Kolkata, West Bengal",
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
        submitted.Status.Should().Be(SellerApplicationStatus.Submitted);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var approveResponse = await _client.PostAsJsonAsync($"/api/admin/seller-applications/{submitted.Id}/review", new ReviewSellerApplicationDto
        {
            Status = SellerApplicationStatus.Approved,
            ReviewNotes = "Approved for Phase 10 product listing test."
        });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        sellerToken = await LoginAsync(sellerEmail);
        JwtTestHelper.GetClaimValues(sellerToken, MarketplaceCapabilityConstants.ClaimType)
            .Should().Contain(MarketplaceCapabilityConstants.ProductSeller);

        SetAuthHeader(sellerToken);
        var createProductResponse = await _client.PostAsJsonAsync("/api/products", new UpsertProductListingDto
        {
            ProductCategoryId = HomeEssentialsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = "Kolkata home cleaning kit",
            Description = "Starter kit for daily home cleaning.",
            Price = 399,
            StockQuantity = 12,
            ImageUrl = "https://example.test/cleaning-kit.jpg",
            IsActive = true
        });
        createProductResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await ReadJsonAsync<ProductListingDto>(createProductResponse);
        product.StoreName.Should().Be("Phase 10 Store");
        product.IsActive.Should().BeTrue();

        var buyerToken = await RegisterAndLoginAsync($"phase10-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);
        var browseResponse = await _client.GetAsync($"/api/products?categoryId={HomeEssentialsCategoryId}&zoneId={SouthKolkataZoneId}");
        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await ReadJsonAsync<List<ProductListingDto>>(browseResponse);
        products.Should().ContainSingle(p => p.Id == product.Id);

        SetAuthHeader(sellerToken);
        var deactivateResponse = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new UpsertProductListingDto
        {
            ProductCategoryId = HomeEssentialsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = product.Title,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ImageUrl = product.ImageUrl,
            IsActive = false
        });
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(buyerToken);
        var afterDeactivateResponse = await _client.GetAsync($"/api/products?categoryId={HomeEssentialsCategoryId}&zoneId={SouthKolkataZoneId}");
        afterDeactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterDeactivate = await ReadJsonAsync<List<ProductListingDto>>(afterDeactivateResponse);
        afterDeactivate.Should().NotContain(p => p.Id == product.Id);
    }

    [Fact]
    public async Task SellerSubmission_RequiresIdentityAndAddressVerification()
    {
        var sellerToken = await RegisterAndLoginAsync($"phase10-incomplete{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(sellerToken);

        var upsertResponse = await _client.PutAsJsonAsync("/api/seller-applications/me", new UpsertSellerApplicationDto
        {
            StoreName = "Incomplete Store",
            PickupAddress = "11 Seller Lane, Kolkata, West Bengal",
            City = "Kolkata",
            State = "West Bengal",
            ServiceZoneId = SouthKolkataZoneId,
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = false
        });
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await _client.PostAsync("/api/seller-applications/me/submit", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var adminEmail = $"phase10-admin{Guid.NewGuid():N}@test.com";
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

    private async Task SeedProductCategoriesAsync()
    {
        await using var dbContext = await _factory.GetDbContextAsync();
        if (await dbContext.ProductCategories.AnyAsync())
            return;

        dbContext.ProductCategories.AddRange(
            new ProductCategory
            {
                Id = HomeEssentialsCategoryId,
                Name = "Home Essentials",
                Slug = "home-essentials",
                Description = "Everyday household products for local buyers",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow
            },
            new ProductCategory
            {
                Id = ToolsCategoryId,
                Name = "Tools And Hardware",
                Slug = "tools-and-hardware",
                Description = "Tools, fittings, and basic hardware supplies",
                IsActive = true,
                SortOrder = 20,
                CreatedAt = DateTime.UtcNow
            });

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
