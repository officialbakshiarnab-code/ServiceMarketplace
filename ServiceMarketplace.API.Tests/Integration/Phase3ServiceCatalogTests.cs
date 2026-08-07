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

public class Phase3ServiceCatalogTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase3ServiceCatalogTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ServiceCatalog_ReturnsSeededActiveCategoriesAndZones()
    {
        var categoriesResponse = await _client.GetAsync("/api/service-catalog/categories");
        var zonesResponse = await _client.GetAsync("/api/service-catalog/zones");

        categoriesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        zonesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await ReadJsonAsync<List<ServiceCategoryDto>>(categoriesResponse);
        var zones = await ReadJsonAsync<List<ServiceZoneDto>>(zonesResponse);

        categories.Should().Contain(c => c.Id == PlumbingCategoryId && c.Name == "Plumbing");
        zones.Should().Contain(z => z.Id == SouthKolkataZoneId && z.DisplayName == "South Kolkata, Kolkata, West Bengal");
    }

    [Fact]
    public async Task CreateRequest_WithCatalogAndStructuredFields_ReturnsNormalizedRequestDetails()
    {
        var customerToken = await RegisterAndLoginAsync($"phase3-customer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(customerToken);

        var preferredStartAt = new DateTime(2026, 8, 8, 10, 30, 0, DateTimeKind.Utc);
        var createResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Fix sink leak",
            Description = "Water is leaking from the cabinet trap.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "24 Exact Road, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Urgent,
            PreferredStartAt = preferredStartAt,
            Requirements = "Bring pipe sealant and basic fittings."
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var mineResponse = await _client.GetAsync("/api/requests/mine");
        mineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requests = await ReadJsonAsync<List<ServiceRequestDto>>(mineResponse);

        var created = requests.Should().ContainSingle(r => r.Title == "Fix sink leak").Subject;
        created.Category.Should().Be("Plumbing");
        created.ServiceCategoryId.Should().Be(PlumbingCategoryId);
        created.ServiceCategoryName.Should().Be("Plumbing");
        created.ServiceZoneId.Should().Be(SouthKolkataZoneId);
        created.ServiceZoneName.Should().Be("South Kolkata, Kolkata, West Bengal");
        created.Urgency.Should().Be(ServiceRequestUrgency.Urgent);
        created.PreferredStartAt.Should().Be(preferredStartAt);
        created.Requirements.Should().Be("Bring pipe sealant and basic fittings.");
    }

    [Fact]
    public async Task ApprovedProviderRequestList_UsesSelectedServiceZoneInsteadOfExactLocation()
    {
        var customerToken = await RegisterAndLoginAsync($"phase3-zone-customer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(customerToken);

        var createResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Install tap",
            Description = "Install a new tap in the wash area.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "99 Private Building, Secret Lane, Kolkata",
            Latitude = 22.501,
            Longitude = 88.361
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerEmail = $"phase3-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        SetAuthHeader(await LoginAsync(providerEmail));

        var availableResponse = await _client.GetAsync("/api/requests/available");
        availableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await availableResponse.Content.ReadAsStringAsync();
        json.Should().Contain("South Kolkata, Kolkata, West Bengal");
        json.Should().NotContain("99 Private Building");
        json.Should().NotContain("Secret Lane");
    }

    [Fact]
    public async Task CreateRequest_WithUnknownCatalogIds_ReturnsBadRequest()
    {
        var customerToken = await RegisterAndLoginAsync($"phase3-invalid{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(customerToken);

        var response = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Invalid catalog request",
            Description = "This should fail because the selected catalog IDs do not exist.",
            ServiceCategoryId = Guid.NewGuid(),
            ServiceZoneId = Guid.NewGuid(),
            Location = "Kolkata, West Bengal",
            Latitude = 22.5726,
            Longitude = 88.3639
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> RegisterAndLoginAsync(string email, string role)
    {
        await RegisterAsync(email, role);
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
        var user = await dbContext.Users.FirstAsync(u => u.NormalizedEmail == normalizedEmail);
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
