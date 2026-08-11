using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class Phase1CapabilityArchitectureTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase1CapabilityArchitectureTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UserToken_IncludesDefaultMarketplaceCapabilities_AndNoAdminPermission()
    {
        var accessToken = await RegisterAndLoginAsync($"cap-user{Guid.NewGuid():N}@test.com", RoleConstants.User);

        var capabilities = JwtTestHelper.GetClaimValues(accessToken, MarketplaceCapabilityConstants.ClaimType);
        capabilities.Should().Contain(MarketplaceCapabilityConstants.ProductBuyer);
        capabilities.Should().Contain(MarketplaceCapabilityConstants.ServiceCustomer);
        capabilities.Should().NotContain(MarketplaceCapabilityConstants.ServiceProvider);

        var permissions = JwtTestHelper.GetClaimValues(accessToken, AdministrativePermissionConstants.ClaimType);
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProviderToken_IncludesCustomerAndProviderCapabilities()
    {
        var email = $"cap-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(email);
        var accessToken = await LoginAsync(email);

        var capabilities = JwtTestHelper.GetClaimValues(accessToken, MarketplaceCapabilityConstants.ClaimType);
        capabilities.Should().Contain(MarketplaceCapabilityConstants.ProductBuyer);
        capabilities.Should().Contain(MarketplaceCapabilityConstants.ServiceCustomer);
        capabilities.Should().Contain(MarketplaceCapabilityConstants.ServiceProvider);
    }

    [Fact]
    public async Task ProviderAccount_CanUseServiceCustomerEndpoints()
    {
        var email = $"provider-customer{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(email);
        SetAuthHeader(await LoginAsync(email));

        var response = await _client.GetAsync("/api/requests/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserAccount_WithoutProviderCapability_CannotUseProviderEndpoints()
    {
        SetAuthHeader(await RegisterAndLoginAsync($"no-provider-cap{Guid.NewGuid():N}@test.com", RoleConstants.User));

        var response = await _client.GetAsync("/api/requests/available");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return JwtTestHelper.ExtractToken(response.Content)
            ?? throw new InvalidOperationException("No token returned");
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
}
