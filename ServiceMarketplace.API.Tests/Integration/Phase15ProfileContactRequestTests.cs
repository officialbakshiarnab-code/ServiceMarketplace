using System.Net;
using System.Net.Http.Headers;
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

public class Phase15ProfileContactRequestTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private static readonly Guid ToolsProductCategoryId = new("cccccccc-cccc-cccc-cccc-ccccccccccc3");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase15ProfileContactRequestTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProfileSearchAndDetail_DoNotExposePhoneOrEmail()
    {
        await SeedCatalogAsync();
        var providerEmail = $"phase15-provider{Guid.NewGuid():N}@test.com";
        var providerToken = await CreateApprovedProviderTokenAsync(providerEmail, "9830000001");
        var userToken = await RegisterAndLoginAsync($"phase15-user{Guid.NewGuid():N}@test.com", RoleConstants.User);

        SetAuthHeader(userToken);
        var searchResponse = await _client.GetAsync($"/api/profiles/search?query={Uri.EscapeDataString(providerEmail)}&profileType={(int)ContactProfileType.Provider}");
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchText = await searchResponse.Content.ReadAsStringAsync();
        searchText.Should().NotContain(providerEmail);
        searchText.Should().NotContain("9830000001");

        var providerUserId = JwtTestHelper.GetClaim(providerToken, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing provider user id claim.");

        var detailResponse = await _client.GetAsync($"/api/profiles/{(int)ContactProfileType.Provider}/{providerUserId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailText = await detailResponse.Content.ReadAsStringAsync();
        detailText.Should().NotContain(providerEmail);
        detailText.Should().NotContain("9830000001");
    }

    [Fact]
    public async Task ContactRequest_RevealsContactOnlyAfterAdminApproval()
    {
        await SeedCatalogAsync();
        var providerEmail = $"phase15-contact-provider{Guid.NewGuid():N}@test.com";
        var providerToken = await CreateApprovedProviderTokenAsync(providerEmail, "9830000002");
        var providerUserId = JwtTestHelper.GetClaim(providerToken, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing provider user id claim.");

        var requesterToken = await RegisterAndLoginAsync($"phase15-requester{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(requesterToken);

        var createResponse = await _client.PostAsJsonAsync("/api/contact-requests", new CreateContactRequestDto
        {
            TargetUserId = providerUserId,
            TargetProfileType = ContactProfileType.Provider,
            Kind = ContactRequestKind.ContactDetails,
            Message = "Please share contact details for a booking discussion."
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = await createResponse.Content.ReadFromJsonAsync<ContactRequestDto>();
        pending.Should().NotBeNull();
        pending!.Status.Should().Be(ContactRequestStatus.PendingAdminReview);
        pending.ContactEmail.Should().BeNull();
        pending.ContactPhone.Should().BeNull();

        var thirdPartyToken = await RegisterAndLoginAsync($"phase15-outsider{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(thirdPartyToken);
        var forbiddenResponse = await _client.GetAsync($"/api/contact-requests/{pending.Id}");
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var approveResponse = await _client.PostAsJsonAsync($"/api/admin/contact-requests/{pending.Id}/review", new ReviewContactRequestDto
        {
            Status = ContactRequestStatus.Approved,
            ReviewNotes = "Approved in contact request integration test."
        });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(requesterToken);
        var approvedResponse = await _client.GetAsync($"/api/contact-requests/{pending.Id}");
        approvedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approvedResponse.Content.ReadFromJsonAsync<ContactRequestDto>();
        approved.Should().NotBeNull();
        approved!.Status.Should().Be(ContactRequestStatus.Approved);
        approved.ContactEmail.Should().Be(providerEmail);
        approved.ContactPhone.Should().Be("9830000002");
    }

    [Fact]
    public async Task CallbackRequest_NotifiesTargetUser()
    {
        await SeedCatalogAsync();
        var providerToken = await CreateApprovedProviderTokenAsync($"phase15-callback-provider{Guid.NewGuid():N}@test.com", "9830000003");
        var providerUserId = JwtTestHelper.GetClaim(providerToken, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing provider user id claim.");

        var requesterToken = await RegisterAndLoginAsync($"phase15-callback-user{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(requesterToken);
        var createResponse = await _client.PostAsJsonAsync("/api/contact-requests", new CreateContactRequestDto
        {
            TargetUserId = providerUserId,
            TargetProfileType = ContactProfileType.Provider,
            Kind = ContactRequestKind.Callback,
            Message = "Please call back tomorrow.",
            PreferredCallbackAt = DateTime.UtcNow.AddDays(1)
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(providerToken);
        var notificationsResponse = await _client.GetAsync("/api/notifications");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<List<UserNotificationDto>>();
        notifications.Should().NotBeNull();
        notifications!.Should().Contain(n => n.Type == UserNotificationType.CallbackRequested);
    }

    [Fact]
    public async Task SellerProfileDetail_UsesSellerUserIdForActiveProductCount()
    {
        await SeedCatalogAsync();
        var sellerToken = await CreateApprovedSellerTokenAsync($"phase15-seller{Guid.NewGuid():N}@test.com");
        var sellerUserId = JwtTestHelper.GetClaim(sellerToken, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing seller user id claim.");
        var userToken = await RegisterAndLoginAsync($"phase15-seller-viewer{Guid.NewGuid():N}@test.com", RoleConstants.User);

        SetAuthHeader(userToken);
        var detailResponse = await _client.GetAsync($"/api/profiles/{(int)ContactProfileType.Seller}/{sellerUserId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailResponse.Content.ReadFromJsonAsync<ProfileDetailDto>();
        detail.Should().NotBeNull();
        detail!.ActiveOfferCount.Should().Be(2);
        detail.Highlights.Should().Contain(h => h.Contains("2 active product", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ContactRequest_DoesNotAllowProviderProfileBypassThroughUserProfileType()
    {
        await SeedCatalogAsync();
        var providerToken = await CreateApprovedProviderTokenAsync($"phase15-bypass-provider{Guid.NewGuid():N}@test.com", "9830000004");
        var providerUserId = JwtTestHelper.GetClaim(providerToken, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing provider user id claim.");

        var requesterToken = await RegisterAndLoginAsync($"phase15-bypass-requester{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(requesterToken);

        var userProfileResponse = await _client.GetAsync($"/api/profiles/{(int)ContactProfileType.User}/{providerUserId}");
        userProfileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var createResponse = await _client.PostAsJsonAsync("/api/contact-requests", new CreateContactRequestDto
        {
            TargetUserId = providerUserId,
            TargetProfileType = ContactProfileType.User,
            Kind = ContactRequestKind.ContactDetails,
            Message = "Trying the plain user route for an approved provider."
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ContactRequest_RejectsDuplicatePendingRequest()
    {
        await SeedCatalogAsync();
        var providerToken = await CreateApprovedProviderTokenAsync($"phase15-duplicate-provider{Guid.NewGuid():N}@test.com", "9830000005");
        var providerUserId = JwtTestHelper.GetClaim(providerToken, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing provider user id claim.");

        var requesterToken = await RegisterAndLoginAsync($"phase15-duplicate-requester{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(requesterToken);

        var request = new CreateContactRequestDto
        {
            TargetUserId = providerUserId,
            TargetProfileType = ContactProfileType.Provider,
            Kind = ContactRequestKind.Callback,
            Message = "Please call when free.",
            PreferredCallbackAt = DateTime.UtcNow.AddHours(3)
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/contact-requests", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateResponse = await _client.PostAsJsonAsync("/api/contact-requests", request);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> CreateApprovedProviderTokenAsync(string email, string phone)
    {
        await RegisterAsync(email, RoleConstants.ServiceProvider);
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(u => u.ServiceProviderProfile)
            .FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        user.PhoneNumber = phone;
        user.NormalizedPhoneNumber = phone;
        user.IsKycSubmitted = true;
        user.IsKycApproved = true;
        user.UpdatedDate = DateTime.UtcNow;

        var profile = user.ServiceProviderProfile
            ?? throw new InvalidOperationException("Provider profile was not created.");

        profile.DisplayName = "Phase 15 Provider";
        profile.BusinessName = "Phase 15 Services";
        profile.Bio = "Approved Kolkata provider for profile search tests.";
        profile.Skills = "Plumbing, repairs, callback workflow";
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
        profile.ReviewCount = 3;
        profile.AverageRating = 4.6m;

        await dbContext.SaveChangesAsync();
        return await LoginAsync(email);
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        var adminEmail = $"phase15-admin{Guid.NewGuid():N}@test.com";
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

    private async Task<string> CreateApprovedSellerTokenAsync(string email)
    {
        await RegisterAsync(email, RoleConstants.User);
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users.FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        var sellerProfile = new SellerProfile
        {
            UserId = user.Id,
            StoreName = "Phase 15 Seller Store",
            BusinessName = "Phase 15 Seller Business",
            Description = "Approved Kolkata seller for profile detail tests.",
            PickupAddress = "Ballygunge, Kolkata",
            City = "Kolkata",
            State = "West Bengal",
            ServiceZoneId = SouthKolkataZoneId,
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = true,
            BusinessVerificationSubmitted = true,
            Status = SellerApplicationStatus.Approved,
            SubmittedAt = DateTime.UtcNow.AddDays(-2),
            ReviewedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        dbContext.SellerProfiles.Add(sellerProfile);
        dbContext.ProductListings.AddRange(
            new ProductListing
            {
                SellerId = user.Id,
                ProductCategoryId = ToolsProductCategoryId,
                ServiceZoneId = SouthKolkataZoneId,
                Title = "Demo Drill Kit",
                Description = "Working drill kit for Kolkata home repairs.",
                Price = 1800,
                StockQuantity = 2,
                Condition = ProductCondition.Good,
                ConditionNotes = "Lightly used and tested.",
                InspectionChecklist = "Chuck, wire, plug, and trigger checked.",
                Status = ProductListingStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new ProductListing
            {
                SellerId = user.Id,
                ProductCategoryId = ToolsProductCategoryId,
                ServiceZoneId = SouthKolkataZoneId,
                Title = "Demo Ladder",
                Description = "Foldable ladder for indoor work.",
                Price = 1200,
                StockQuantity = 1,
                Condition = ProductCondition.Good,
                ConditionNotes = "Stable hinges and clean steps.",
                InspectionChecklist = "Hinges, locks, and rubber feet checked.",
                Status = ProductListingStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new ProductListing
            {
                SellerId = user.Id,
                ProductCategoryId = ToolsProductCategoryId,
                ServiceZoneId = SouthKolkataZoneId,
                Title = "Inactive Demo Tool",
                Description = "Inactive listing should not count.",
                Price = 900,
                StockQuantity = 1,
                Status = ProductListingStatus.Inactive,
                CreatedAt = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();
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
                Description = "Water and fitting repairs",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await dbContext.ProductCategories.AnyAsync(c => c.Id == ToolsProductCategoryId))
        {
            dbContext.ProductCategories.Add(new ProductCategory
            {
                Id = ToolsProductCategoryId,
                Name = "Tools And Hardware",
                Slug = "phase15-tools-hardware",
                Description = "Tools for seller profile count tests",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
