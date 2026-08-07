using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class Phase9ServicePackagesTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid ElectricalCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase9ServicePackagesTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ApprovedProvider_CanCreatePackage_AndCustomerCanBookFixedPriceOrder()
    {
        var providerEmail = $"phase9-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        var providerToken = await LoginAsync(providerEmail);

        SetAuthHeader(providerToken);
        var createPackageResponse = await _client.PostAsJsonAsync("/api/service-packages", new UpsertServicePackageDto
        {
            Title = "Tap leak repair package",
            Description = "Fixed visit for one tap leak repair with basic seal replacement.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Price = 799,
            EstimatedDurationMinutes = 60,
            IsActive = true
        });
        createPackageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var package = await ReadJsonAsync<ServicePackageDto>(createPackageResponse);
        package.ProviderDisplayName.Should().Be("Phase 9 Provider");
        package.Price.Should().Be(799);

        var customerToken = await RegisterAndLoginAsync($"phase9-customer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(customerToken);
        var activeResponse = await _client.GetAsync($"/api/service-packages?categoryId={PlumbingCategoryId}&zoneId={SouthKolkataZoneId}");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activePackages = await ReadJsonAsync<List<ServicePackageDto>>(activeResponse);
        activePackages.Should().ContainSingle(p => p.Id == package.Id);

        var bookResponse = await _client.PostAsJsonAsync($"/api/service-packages/{package.Id}/book", new BookServicePackageDto
        {
            ScheduledStartAt = DateTime.UtcNow.AddHours(6),
            Location = "99 Phase Nine Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Requirements = "Please call before arrival."
        });
        bookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await ReadJsonAsync<ServiceOrderDto>(bookResponse);
        order.AcceptedBidId.Should().BeNull();
        order.ServicePackageId.Should().Be(package.Id);
        order.AgreedAmount.Should().Be(package.Price);
        order.Status.Should().Be(ServiceOrderStatus.PendingStart);

        SetAuthHeader(providerToken);
        var providerOrdersResponse = await _client.GetAsync("/api/orders/provider");
        providerOrdersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerOrders = await ReadJsonAsync<List<ServiceOrderDto>>(providerOrdersResponse);
        providerOrders.Should().ContainSingle(o => o.Id == order.Id && o.ServicePackageId == package.Id);

        var startResponse = await _client.PostAsync($"/api/orders/{order.Id}/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerCompleteResponse = await _client.PostAsync($"/api/orders/{order.Id}/provider-complete", null);
        providerCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(customerToken);
        var paymentResponse = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.Cash,
            Amount = package.Price,
            Notes = "Paid after fixed-price package completion."
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmResponse = await _client.PostAsync($"/api/orders/{order.Id}/confirm-complete", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewResponse = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/review", new CreateServiceOrderReviewDto
        {
            Rating = 5,
            Feedback = "Clear fixed price and smooth booking."
        });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auditResponse = await _client.GetAsync($"/api/orders/{order.Id}/audit");
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditEvents = await ReadJsonAsync<List<ServiceOrderAuditEventDto>>(auditResponse);
        auditEvents.Select(e => e.EventType).Should().Contain(new[]
        {
            "ServiceOrderCreatedFromPackage",
            "ServiceOrderStarted",
            "ServiceOrderProviderCompleted",
            "ServiceOrderPaymentRecorded",
            "ServiceOrderCompleted",
            "ServiceOrderReviewCreated"
        });
    }

    [Fact]
    public async Task PackageManagement_RequiresApprovedProviderCoverage()
    {
        var providerEmail = $"phase9-unapproved{Guid.NewGuid():N}@test.com";
        var providerToken = await RegisterAndLoginAsync(providerEmail, RoleConstants.ServiceProvider);

        SetAuthHeader(providerToken);
        var unapprovedResponse = await _client.PostAsJsonAsync("/api/service-packages", new UpsertServicePackageDto
        {
            Title = "Unapproved package",
            Description = "Should be rejected because provider is not approved.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Price = 500,
            EstimatedDurationMinutes = 45,
            IsActive = true
        });
        unapprovedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await ApproveProviderAsync(providerEmail);
        providerToken = await LoginAsync(providerEmail);
        SetAuthHeader(providerToken);

        var wrongCategoryResponse = await _client.PostAsJsonAsync("/api/service-packages", new UpsertServicePackageDto
        {
            Title = "Electrical package",
            Description = "Provider is approved for plumbing, not electrical.",
            ServiceCategoryId = ElectricalCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Price = 500,
            EstimatedDurationMinutes = 45,
            IsActive = true
        });
        wrongCategoryResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Customer_CannotBookOwnProviderPackage()
    {
        var providerEmail = $"phase9-both{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.Both);
        await ApproveProviderAsync(providerEmail);
        var providerToken = await LoginAsync(providerEmail);

        SetAuthHeader(providerToken);
        var createPackageResponse = await _client.PostAsJsonAsync("/api/service-packages", new UpsertServicePackageDto
        {
            Title = "Own package",
            Description = "Provider should not be able to book this as customer.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Price = 700,
            EstimatedDurationMinutes = 60,
            IsActive = true
        });
        createPackageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var package = await ReadJsonAsync<ServicePackageDto>(createPackageResponse);

        var bookResponse = await _client.PostAsJsonAsync($"/api/service-packages/{package.Id}/book", new BookServicePackageDto
        {
            ScheduledStartAt = DateTime.UtcNow.AddHours(2),
            Location = "Own Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361
        });
        bookResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private async Task ApproveProviderAsync(string email)
    {
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(u => u.ServiceProviderProfile)
            .FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        var profile = user.ServiceProviderProfile
            ?? throw new InvalidOperationException("Provider profile was not created.");

        profile.DisplayName = "Phase 9 Provider";
        profile.BusinessName = "Phase 9 Services";
        profile.Skills = "Plumbing, package repairs";
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
