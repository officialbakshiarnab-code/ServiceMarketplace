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

public class Phase14UnifiedMarketplaceSearchTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid HomeEssentialsCategoryId = new("cccccccc-cccc-cccc-cccc-ccccccccccc1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase14UnifiedMarketplaceSearchTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnifiedSearch_ReturnsMatchingServicePackagesAndProducts()
    {
        await SeedCatalogAsync();
        var providerToken = await CreateApprovedProviderTokenAsync($"phase14-provider{Guid.NewGuid():N}@test.com");
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase14-seller{Guid.NewGuid():N}@test.com");

        var servicePackage = await CreateServicePackageAsync(providerToken, "Kitchen sink repair package", 850m);
        var product = await CreateProductListingAsync(sellerToken, "Kitchen sink wrench kit", 320m, ProductCondition.New);

        var buyerToken = await RegisterAndLoginAsync($"phase14-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);

        var response = await _client.GetAsync($"/api/marketplace/search?q=sink&zoneId={SouthKolkataZoneId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await ReadJsonAsync<MarketplaceSearchResponse>(response);
        results.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        results.Items.Should().Contain(i => i.Type == MarketplaceSearchItemType.ServicePackage && i.Id == servicePackage.Id);
        results.Items.Should().Contain(i => i.Type == MarketplaceSearchItemType.ProductListing && i.Id == product.Id);
        results.ServicePackageCount.Should().BeGreaterThanOrEqualTo(1);
        results.ProductListingCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task UnifiedSearch_AppliesTypePriceAndConditionFilters()
    {
        await SeedCatalogAsync();
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase14-filter-seller{Guid.NewGuid():N}@test.com");
        var goodProduct = await CreateProductListingAsync(sellerToken, "Used drill search filter", 450m, ProductCondition.Good);
        _ = await CreateProductListingAsync(sellerToken, "New drill search filter", 900m, ProductCondition.New);

        var buyerToken = await RegisterAndLoginAsync($"phase14-filter-buyer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(buyerToken);

        var response = await _client.GetAsync($"/api/marketplace/search?q=drill&type={(int)MarketplaceSearchItemType.ProductListing}&productCondition={(int)ProductCondition.Good}&maxPrice=500");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await ReadJsonAsync<MarketplaceSearchResponse>(response);
        results.Items.Should().ContainSingle(i => i.Id == goodProduct.Id);
        results.Items.Should().OnlyContain(i => i.Type == MarketplaceSearchItemType.ProductListing);
        results.Items.Should().OnlyContain(i => i.Price <= 500m && i.ProductCondition == ProductCondition.Good);
    }

    [Fact]
    public async Task UnifiedSearch_RequiresAuthenticatedCustomerCapability()
    {
        var anonymousResponse = await _client.GetAsync("/api/marketplace/search?q=sink");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<ServicePackageDto> CreateServicePackageAsync(string providerToken, string title, decimal price)
    {
        SetAuthHeader(providerToken);
        var response = await _client.PostAsJsonAsync("/api/service-packages", new UpsertServicePackageDto
        {
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = title,
            Description = "Search-visible service package for Phase 14.",
            Price = price,
            EstimatedDurationMinutes = 90,
            IsActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync<ServicePackageDto>(response);
    }

    private async Task<ProductListingDto> CreateProductListingAsync(string sellerToken, string title, decimal price, ProductCondition condition)
    {
        SetAuthHeader(sellerToken);
        var dto = new UpsertProductListingDto
        {
            ProductCategoryId = HomeEssentialsCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Title = title,
            Description = "Search-visible product listing for Phase 14.",
            Price = price,
            StockQuantity = 3,
            ImageUrl = "https://example.test/search-product.jpg",
            Condition = condition,
            IsActive = true
        };

        if (condition != ProductCondition.New)
        {
            dto.ConditionNotes = "Used but working well with visible disclosure.";
            dto.InspectionChecklist = "Checked condition, parts, and safe operation.";
            dto.PurchaseYear = 2024;
        }

        var response = await _client.PostAsJsonAsync("/api/products", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync<ProductListingDto>(response);
    }

    private async Task<string> CreateApprovedProviderTokenAsync(string email)
    {
        await RegisterAsync(email, RoleConstants.ServiceProvider);
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(u => u.ServiceProviderProfile)
            .FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        var profile = user.ServiceProviderProfile
            ?? throw new InvalidOperationException("Provider profile was not created.");

        profile.DisplayName = "Phase 14 Search Provider";
        profile.BusinessName = "Phase 14 Services";
        profile.Skills = "Plumbing and search fixtures";
        profile.PrimaryCategory = "Plumbing";
        profile.ServiceCategoryId = PlumbingCategoryId;
        profile.ServiceAreaCity = "Kolkata";
        profile.ServiceAreaState = "West Bengal";
        profile.ServiceAreaZone = "South Kolkata";
        profile.ServiceZoneId = SouthKolkataZoneId;
        profile.HourlyRate = 500;
        profile.IsAvailable = true;
        profile.IdentityVerificationSubmitted = true;
        profile.AddressVerificationSubmitted = true;
        profile.BackgroundCheckConsent = true;
        profile.Status = ProviderApplicationStatus.Approved;
        user.IsKycSubmitted = true;
        user.IsKycApproved = true;
        user.UpdatedDate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return await LoginAsync(email);
    }

    private async Task<string> CreateApprovedSellerTokenAsync(string email)
    {
        await RegisterAsync(email, RoleConstants.User);
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users.FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            UserId = user.Id,
            StoreName = "Phase 14 Search Store",
            BusinessName = "Phase 14 Products",
            Description = "Approved seller for unified search tests.",
            PickupAddress = "14 Search Lane, Kolkata, West Bengal",
            City = "Kolkata",
            State = "West Bengal",
            ServiceZoneId = SouthKolkataZoneId,
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = true,
            BusinessVerificationSubmitted = true,
            Status = SellerApplicationStatus.Approved,
            SubmittedAt = DateTime.UtcNow,
            ReviewedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
        return await LoginAsync(email);
    }

    private async Task SeedCatalogAsync()
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

        if (!await dbContext.ServiceCategories.AnyAsync(c => c.Id == PlumbingCategoryId))
        {
            dbContext.ServiceCategories.Add(new ServiceCategory
            {
                Id = PlumbingCategoryId,
                Name = "Plumbing",
                Slug = "plumbing",
                Description = "Leaks, taps, pipes, fittings, and water-flow issues",
                IsActive = true,
                SortOrder = 10,
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
