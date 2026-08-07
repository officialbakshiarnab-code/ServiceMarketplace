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
using ServiceMarketplace.Domain.Enums;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class Phase2ProviderOnboardingTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private const string TestJwtKey = "TEST_ONLY_JWT_SIGNING_KEY_32_BYTES_MINIMUM_12345";

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase2ProviderOnboardingTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProviderRegistration_CreatesDraftProviderApplication()
    {
        var email = $"provider-draft{Guid.NewGuid():N}@test.com";

        await RegisterAsync(email, RoleConstants.ServiceProvider);

        await using var dbContext = await _factory.GetDbContextAsync();
        var user = await dbContext.Users.FirstAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        var profile = await dbContext.ServiceProviderProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);

        profile.Should().NotBeNull();
        profile!.Status.Should().Be(ProviderApplicationStatus.Draft);
        profile.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProviderApplication_SubmitAndAdminApprove_EnablesProviderWorkflow()
    {
        var email = $"provider-apply{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email, RoleConstants.User);
        SetAuthHeader(await LoginAsync(email));

        var upsertResponse = await _client.PutAsJsonAsync("/api/provider-applications/me", BuildApplication());
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await _client.PostAsync("/api/provider-applications/me/submit", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await ReadJsonAsync<ProviderApplicationDto>(submitResponse);
        submitted.Status.Should().Be(ProviderApplicationStatus.Submitted);

        SetAuthHeader(CreateAdminToken());
        var review = new ReviewProviderApplicationDto
        {
            Status = ProviderApplicationStatus.Approved,
            ReviewNotes = "Approved for service pilot."
        };
        var reviewResponse = await _client.PostAsJsonAsync($"/api/admin/provider-applications/{submitted.Id}/review", review);
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(await LoginAsync(email));
        var availableResponse = await _client.GetAsync("/api/requests/available");
        availableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var accessToken = await LoginAsync(email);
        var capabilities = JwtTestHelper.GetClaimValues(accessToken, MarketplaceCapabilityConstants.ClaimType);
        capabilities.Should().Contain(MarketplaceCapabilityConstants.ServiceProvider);
    }

    [Fact]
    public async Task ProviderApplication_MissingVerification_CannotSubmit()
    {
        var email = $"provider-incomplete{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email, RoleConstants.User);
        SetAuthHeader(await LoginAsync(email));

        var dto = BuildApplication();
        dto.IdentityVerificationSubmitted = false;

        var upsertResponse = await _client.PutAsJsonAsync("/api/provider-applications/me", dto);
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await _client.PostAsync("/api/provider-applications/me/submit", null);

        submitResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApprovedProviderRequestList_UsesCoarseLocationOnly()
    {
        var customerToken = await RegisterAndLoginAsync($"phase2-customer{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(customerToken);

        var createResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Fix a kitchen tap",
            Description = "Tap has a persistent leak.",
            Category = "Plumbing",
            Location = "221B Exact Lane, Kolkata, West Bengal",
            Latitude = 22.5726,
            Longitude = 88.3639
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerEmail = $"phase2-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.User);
        SetAuthHeader(await LoginAsync(providerEmail));
        var upsertResponse = await _client.PutAsJsonAsync("/api/provider-applications/me", BuildApplication());
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitResponse = await _client.PostAsync("/api/provider-applications/me/submit", null);
        var submitted = await ReadJsonAsync<ProviderApplicationDto>(submitResponse);

        SetAuthHeader(CreateAdminToken());
        await _client.PostAsJsonAsync($"/api/admin/provider-applications/{submitted.Id}/review", new ReviewProviderApplicationDto
        {
            Status = ProviderApplicationStatus.Approved
        });

        SetAuthHeader(await LoginAsync(providerEmail));
        var response = await _client.GetAsync("/api/requests/available");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Kolkata, West Bengal");
        json.Should().NotContain("221B Exact Lane");
    }

    private static UpsertProviderApplicationDto BuildApplication()
    {
        return new UpsertProviderApplicationDto
        {
            DisplayName = "Trusted Plumber",
            BusinessName = "Trusted Plumbing Services",
            Bio = "Residential repair specialist.",
            Skills = "Plumbing, leak repair",
            PrimaryCategory = "Plumbing",
            ServiceAreaCity = "Kolkata",
            ServiceAreaState = "West Bengal",
            ServiceAreaZone = "South Kolkata",
            HourlyRate = 450,
            IsAvailable = true,
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = true,
            BackgroundCheckConsent = true
        };
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

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Response body could not be deserialized.");
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "admin@test.local"),
            new Claim(ClaimTypes.Role, RoleConstants.Admin),
            new Claim("role", RoleConstants.Admin),
            new Claim("UserType", "3"),
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
