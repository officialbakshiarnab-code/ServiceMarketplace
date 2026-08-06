using System.Net;
using FluentAssertions;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

/// <summary>
/// Integration tests for token refresh and idempotency.
/// Verifies refresh token functionality and idempotent behavior.
/// </summary>
public class TokenRefreshIdempotencyTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TokenRefreshIdempotencyTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewAccessToken()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync("refresh@test.com");

        var request = new
        {
            refreshToken = refreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAccessToken = JwtTestHelper.ExtractToken(response.Content);
        newAccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            refreshToken = "invalid.refresh.token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithoutToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            refreshToken = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_ReturnsNewRefreshToken()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync("newrefresh@test.com");

        var request = new
        {
            refreshToken = refreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newRefreshToken = JwtTestHelper.ExtractRefreshToken(response.Content);
        newRefreshToken.Should().NotBeNullOrWhiteSpace();
        newRefreshToken.Should().NotBe(refreshToken); // Token should be rotated
    }

    [Fact]
    public async Task RefreshToken_Idempotency_SameTokenSubmittedTwice()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync("idempotent@test.com");

        var request = new
        {
            refreshToken = refreshToken
        };

        // Act - First refresh
        var response1 = await _client.PostAsJsonAsync("/api/auth/refresh", request);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Second refresh with same token (within 30 seconds)
        var response2 = await _client.PostAsJsonAsync("/api/auth/refresh", request);

        // Assert - Should both succeed or second should fail gracefully
        // Due to idempotency guarantee, at least one should succeed
        response2.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshedToken_CanAccessProtectedResources()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync("accesstest@test.com");

        var refreshRequest = new
        {
            refreshToken = refreshToken
        };

        // Act - Refresh token
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAccessToken = JwtTestHelper.ExtractToken(refreshResponse.Content);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAccessToken);

        // Act - Access protected resource with new token
        var protectedResponse = await _client.GetAsync("/api/requests/mine");

        // Assert
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RefreshedToken_HasValidExpiration()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync("exptest@test.com");

        var request = new
        {
            refreshToken = refreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);
        var newAccessToken = JwtTestHelper.ExtractToken(response.Content);

        // Assert
        var expiration = JwtTestHelper.GetTokenExpiration(newAccessToken!);
        var now = DateTime.UtcNow;

        // New token should expire in approximately 10 minutes
        expiration.Should().BeCloseTo(now.AddMinutes(10), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RefreshedToken_ContainsCorrectClaims()
    {
        // Arrange
        var email = "claimtest@test.com";
        var (_, refreshToken) = await RegisterAndLoginAsync(email);

        var request = new
        {
            refreshToken = refreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", request);
        var newAccessToken = JwtTestHelper.ExtractToken(response.Content);

        // Assert
        var claims = JwtTestHelper.GetClaims(newAccessToken!);
        claims.Should().ContainKey("sub");
        claims.Should().ContainKey("email");
        claims.Should().ContainKey("role");
        claims.Should().ContainKey("jti");
        
        claims["email"].Should().Contain(email.Split('@')[0]); // Email should be present
    }

    [Fact]
    public async Task OldRefreshToken_AfterRotation_ShouldFail()
    {
        // Arrange
        var (_, refreshToken) = await RegisterAndLoginAsync("rotationtest@test.com");

        var firstRequest = new { refreshToken = refreshToken };

        // Act - First refresh (rotates token)
        var firstRefresh = await _client.PostAsJsonAsync("/api/auth/refresh", firstRequest);
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldRefreshToken = refreshToken; // Keep reference to old token

        // Act - Try to use old token again
        var secondRequest = new { refreshToken = oldRefreshToken };
        var secondRefresh = await _client.PostAsJsonAsync("/api/auth/refresh", secondRequest);

        // Assert - Old token should be rejected (token rotation security)
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string accessToken, string refreshToken)> RegisterAndLoginAsync(string email)
    {
        // Register
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
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
