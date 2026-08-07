using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class Phase0SecurityHardeningTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>, IAsyncLifetime
{
    private const string TestJwtKey = "TEST_ONLY_JWT_SIGNING_KEY_32_BYTES_MINIMUM_12345";
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private Infrastructure.Data.AppDbContext _dbContext = null!;

    public Phase0SecurityHardeningTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _dbContext = await _factory.GetDbContextAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Register_WithAdminRole_ReturnsBadRequest_AndDoesNotCreateAdminUser()
    {
        var request = AuthRequestBuilder.CreateDefault()
            .WithEmail($"admin-self-register{Guid.NewGuid():N}@test.com")
            .WithRole(RoleConstants.Admin)
            .BuildRegisterRequest();

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var adminExists = await _dbContext.Users.AnyAsync(u => u.NormalizedEmail == request.Email.ToUpperInvariant());
        adminExists.Should().BeFalse();
    }

    [Fact]
    public async Task ProviderAvailableRequests_WithUnapprovedProvider_ReturnsForbidden()
    {
        var providerToken = await RegisterAndLoginAsync($"unapproved-provider{Guid.NewGuid():N}@test.com", RoleConstants.ServiceProvider);
        SetAuthHeader(providerToken);

        var response = await _client.GetAsync("/api/requests/available");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlaceBid_WithUnapprovedProvider_ReturnsForbidden()
    {
        var requestId = await CreateCustomerRequestAsync();
        var providerToken = await RegisterAndLoginAsync($"unapproved-bidder{Guid.NewGuid():N}@test.com", RoleConstants.ServiceProvider);
        SetAuthHeader(providerToken);

        var bid = new CreateBidDto
        {
            ServiceRequestId = requestId,
            Amount = 500,
            ProposedDateTime = DateTime.UtcNow.AddHours(3),
            Message = "I can do this work."
        };

        var response = await _client.PostAsJsonAsync("/api/bids", bid);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProviderAvailableRequests_WithApprovedProvider_DoesNotExposeExactLocationOrCustomerId()
    {
        await CreateCustomerRequestAsync(
            location: "Flat 7B, 123 Exact Street, Kolkata",
            latitude: 22.572645,
            longitude: 88.363892);

        var providerEmail = $"approved-provider{Guid.NewGuid():N}@test.com";
        var providerToken = await RegisterAndLoginAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        SetAuthHeader(providerToken);

        var response = await _client.GetAsync("/api/requests/available");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.EnumerateArray().First();

        item.TryGetProperty("customerId", out _).Should().BeFalse();
        item.TryGetProperty("location", out _).Should().BeFalse();
        item.TryGetProperty("latitude", out _).Should().BeFalse();
        item.TryGetProperty("longitude", out _).Should().BeFalse();
        item.GetProperty("approximateLocation").GetString()
            .Should().NotBe("Flat 7B, 123 Exact Street, Kolkata");
    }

    [Fact]
    public async Task AdminKpis_RequireAdminPermission()
    {
        var userToken = await RegisterAndLoginAsync($"not-admin{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(userToken);

        var userResponse = await _client.GetAsync("/api/admin/kpis");
        userResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        SetAuthHeader(CreateSignedToken(RoleConstants.Admin, "3"));
        var adminResponse = await _client.GetAsync("/api/admin/kpis");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateCustomerRequestAsync(
        string location = "Customer exact address",
        double latitude = 22.57,
        double longitude = 88.36)
    {
        var userToken = await RegisterAndLoginAsync($"customer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(userToken);

        var request = new CreateServiceRequestDto
        {
            Title = "Repair refrigerator",
            Description = "The refrigerator is not cooling.",
            Category = "Appliance Repair",
            Location = location,
            Latitude = latitude,
            Longitude = longitude
        };

        var response = await _client.PostAsJsonAsync("/api/requests", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("requestId").GetGuid();
    }

    private async Task<string> RegisterAndLoginAsync(string email, string role)
    {
        var register = AuthRequestBuilder.CreateDefault()
            .WithEmail(email)
            .WithRole(role)
            .BuildRegisterRequest();

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", register);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return JwtTestHelper.ExtractToken(loginResponse.Content)
            ?? throw new InvalidOperationException("No access token returned");
    }

    private async Task ApproveProviderAsync(string email)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await _dbContext.Users.FirstAsync(u => u.NormalizedEmail == normalizedEmail);
        user.IsKycSubmitted = true;
        user.IsKycApproved = true;
        user.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string CreateSignedToken(string role, string userType)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "admin@test.local"),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim("UserType", userType),
            new Claim(AdministrativePermissionConstants.ClaimType, AdministrativePermissionConstants.PlatformAdmin),
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: "ServiceMarketplace",
            audience: "ServiceMarketplaceUsers",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
