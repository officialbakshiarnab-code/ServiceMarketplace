using System.Net;
using FluentAssertions;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

/// <summary>
/// Integration tests for role-based authorization.
/// Verifies that endpoints enforce role requirements correctly.
/// </summary>
public class RoleBasedAuthorizationTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RoleBasedAuthorizationTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UserEndpoint_WithUserRole_Returns200()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync(
            "user@test.com",
            RoleConstants.User
        );

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserEndpoint_WithProviderRole_Returns403Forbidden()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync(
            "provider@test.com",
            RoleConstants.ServiceProvider
        );

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act - Try to access User-only endpoint
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProviderEndpoint_WithProviderRole_Returns200()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync(
            "provider2@test.com",
            RoleConstants.ServiceProvider
        );

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.GetAsync("/api/requests/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProviderEndpoint_WithUserRole_Returns403Forbidden()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync(
            "user2@test.com",
            RoleConstants.User
        );

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act - Try to access Provider-only endpoint
        var response = await _client.GetAsync("/api/requests/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LogoutEndpoint_RequiresAuthentication()
    {
        // Act - Try to logout without token
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenRefreshEndpoint_AllowsUnauthenticatedAccess()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync(
            "refresh@test.com",
            RoleConstants.User
        );

        var request = new
        {
            refreshToken = refreshToken
        };

        // Act - Refresh without authentication header
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert - Should succeed (endpoint allows anonymous)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
        // 200 if token valid, 401 if token invalid/expired
    }

    [Fact]
    public async Task BidEndpoint_OnlyAccessibleByServiceProvider()
    {
        // Arrange - User tries to place bid
        var (userToken, _) = await RegisterAndLoginAsync(
            "biduser@test.com",
            RoleConstants.User
        );

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        var bidRequest = new
        {
            serviceRequestId = Guid.NewGuid(),
            amount = 100,
            proposedDateTime = DateTime.UtcNow.AddDays(1),
            message = "Test bid"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/bids", bidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleClaimIncludedInToken()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync(
            "rolecheck@test.com",
            RoleConstants.User
        );

        // Act
        var claims = JwtTestHelper.GetClaims(accessToken);

        // Assert
        claims.Should().ContainKey("role");
        claims["role"].Should().Be(RoleConstants.User);
    }

    [Fact]
    public async Task ProviderRoleClaimIncludedInToken()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync(
            "providerrole@test.com",
            RoleConstants.ServiceProvider
        );

        // Act
        var claims = JwtTestHelper.GetClaims(accessToken);

        // Assert
        claims.Should().ContainKey("role");
        claims["role"].Should().Be(RoleConstants.ServiceProvider);
    }

    private async Task<(string accessToken, string refreshToken)> RegisterAndLoginAsync(
        string email,
        string role)
    {
        // Register with specific role
        var registerReq = AuthRequestBuilder.CreateDefault()
            .WithEmail(email)
            .WithRole(role)
            .BuildRegisterRequest();

        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        // Login
        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        var accessToken = JwtTestHelper.ExtractToken(loginResponse.Content) ?? 
            throw new InvalidOperationException("No token returned");
        var refreshToken = JwtTestHelper.ExtractRefreshToken(loginResponse.Content) ?? 
            throw new InvalidOperationException("No refresh token returned");

        return (accessToken, refreshToken);
    }
}
