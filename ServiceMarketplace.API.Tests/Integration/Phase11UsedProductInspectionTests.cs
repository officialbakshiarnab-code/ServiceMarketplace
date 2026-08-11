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

public class Phase11UsedProductInspectionTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid ElectronicsCategoryId = new("cccccccc-cccc-cccc-cccc-ccccccccccc3");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase11UsedProductInspectionTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProductCatalog_ReturnsCategorySpecificUsedInspectionPrompts()
    {
        await SeedProductCatalogAsync();

        var response = await _client.GetAsync("/api/product-catalog/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await ReadJsonAsync<List<ProductCategoryDto>>(response);
        var electronics = categories.Should().ContainSingle(c => c.Id == ElectronicsCategoryId).Subject;
        electronics.UsedInspectionPrompts.Should().Contain(p => p.Contains("battery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UsedProductListings_RequireConditionDisclosure_AndCanBeFilteredByCondition()
    {
        await SeedProductCatalogAsync();
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase11-seller{Guid.NewGuid():N}@test.com");

        SetAuthHeader(sellerToken);
        var blockedResponse = await _client.PostAsJsonAsync("/api/products", new UpsertProductListingDto
        {
            ProductCategoryId = ElectronicsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = "Used tablet missing inspection",
            Description = "A used tablet without transparent condition notes.",
            Price = 3200,
            StockQuantity = 1,
            Condition = ProductCondition.Good,
            IsActive = true
        });
        blockedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var createResponse = await _client.PostAsJsonAsync("/api/products", new UpsertProductListingDto
        {
            ProductCategoryId = ElectronicsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = "Used tablet",
            Description = "A working used tablet for local pickup.",
            Price = 3200,
            StockQuantity = 1,
            ImageUrl = "https://example.test/tablet.jpg",
            Condition = ProductCondition.Good,
            ConditionNotes = "Minor scratches on the back, display is clean, charger included.",
            InspectionChecklist = "Powered on, touch checked, charging port checked, factory reset completed.",
            PurchaseYear = 2024,
            HasOriginalBill = true,
            HasWarranty = false,
            IsActive = true
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await ReadJsonAsync<ProductListingDto>(createResponse);
        created.IsUsed.Should().BeTrue();
        created.Condition.Should().Be(ProductCondition.Good);
        created.ConditionLabel.Should().Be("Good");
        created.UsedInspectionPrompts.Should().Contain(p => p.Contains("Power on", StringComparison.OrdinalIgnoreCase));

        var buyerToken = await RegisterAndLoginAsync($"phase11-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);

        var goodResponse = await _client.GetAsync($"/api/products?categoryId={ElectronicsCategoryId}&zoneId={SouthKolkataZoneId}&condition={(int)ProductCondition.Good}");
        goodResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var goodProducts = await ReadJsonAsync<List<ProductListingDto>>(goodResponse);
        goodProducts.Should().ContainSingle(p => p.Id == created.Id);

        var newResponse = await _client.GetAsync($"/api/products?categoryId={ElectronicsCategoryId}&zoneId={SouthKolkataZoneId}&condition={(int)ProductCondition.New}");
        newResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newProducts = await ReadJsonAsync<List<ProductListingDto>>(newResponse);
        newProducts.Should().NotContain(p => p.Id == created.Id);
    }

    private async Task<string> CreateApprovedSellerTokenAsync(string email)
    {
        var sellerToken = await RegisterAndLoginAsync(email, RoleConstants.User);
        SetAuthHeader(sellerToken);

        var upsertResponse = await _client.PutAsJsonAsync("/api/seller-applications/me", new UpsertSellerApplicationDto
        {
            StoreName = "Phase 11 Used Goods",
            BusinessName = "Phase 11 Commerce",
            Description = "Used product pilot seller.",
            PickupAddress = "12 Seller Lane, Kolkata, West Bengal",
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
            ReviewNotes = "Approved for Phase 11 used product test."
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
        var adminEmail = $"phase11-admin{Guid.NewGuid():N}@test.com";
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
        if (!await dbContext.ProductCategories.AnyAsync(c => c.Id == ElectronicsCategoryId))
        {
            dbContext.ProductCategories.Add(new ProductCategory
            {
                Id = ElectronicsCategoryId,
                Name = "Electronics",
                Slug = "electronics",
                Description = "Small electronics and accessories",
                IsActive = true,
                SortOrder = 30,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await dbContext.ProductInspectionPrompts.AnyAsync(p => p.ProductCategoryId == ElectronicsCategoryId))
        {
            dbContext.ProductInspectionPrompts.Add(new ProductInspectionPrompt
            {
                Id = new Guid("dddddddd-dddd-dddd-dddd-ddddddddddd3"),
                ProductCategoryId = ElectronicsCategoryId,
                Prompt = "Power on the device, check battery/charging, ports, display, buttons, invoice/warranty status, and reset/lock status.",
                SortOrder = 10,
                IsActive = true,
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
