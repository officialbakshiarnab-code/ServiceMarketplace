using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

/// <summary>
/// Integration tests for complete authentication flow:
/// Register ? Login ? Refresh ? Logout
/// 
/// These tests verify the end-to-end authentication pipeline using
/// WebApplicationFactory with in-memory database.
/// </summary>
public class AuthenticationFlowTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationFlowTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidCredentials_CreatesNewUser()
    {
        // Arrange
        var request = AuthRequestBuilder.CreateUser("newuser@test.com").BuildRegisterRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("successfully");
    }

    [Fact]
    public async Task Register_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange
        var request = AuthRequestBuilder.CreateUser()
            .WithRole("InvalidRole")
            .BuildRegisterRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsOk()
    {
        // Arrange - Register first user
        var user = AuthRequestBuilder.CreateUser("duplicate@test.com").BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", user);

        // Act - Try to register with same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", user);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtToken()
    {
        // Arrange
        var registerReq = AuthRequestBuilder.CreateUser("logintest@test.com").BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = "logintest@test.com",
            Password = "Test@123456"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = JwtTestHelper.ExtractToken(response.Content);
        token.Should().NotBeNullOrWhiteSpace();
        
        // Verify token structure
        var claims = JwtTestHelper.GetClaims(token!);
        claims.Should().ContainKey("sub"); // User ID
        claims.Should().ContainKey("email");
        claims.Should().ContainKey("role");
        claims.Should().ContainKey("jti"); // Session ID
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var registerReq = AuthRequestBuilder.CreateUser("invalidpwd@test.com").BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = "invalidpwd@test.com",
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ReturnsTokenWithCorrectExpiration()
    {
        // Arrange
        var registerReq = AuthRequestBuilder.CreateUser("tokenexp@test.com").BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = "tokenexp@test.com",
            Password = "Test@123456"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        var token = JwtTestHelper.ExtractToken(response.Content);

        // Assert
        var expiration = JwtTestHelper.GetTokenExpiration(token!);
        var now = DateTime.UtcNow;
        
        // Token should expire in approximately 10 minutes
        expiration.Should().BeCloseTo(now.AddMinutes(10), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_ReturnsRefreshToken()
    {
        // Arrange
        var registerReq = AuthRequestBuilder.CreateUser("refresh@test.com").BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = "refresh@test.com",
            Password = "Test@123456"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.Should().HaveProperty("refreshToken");
    }

    [Fact]
    public async Task Logout_WithValidToken_Returns200()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync("logout@test.com");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        // Arrange
        var (accessToken, _) = await RegisterAndLoginAsync("protected@test.com");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token");

        // Act
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteAuthFlow_RegisterLoginLogout()
    {
        // Arrange & Act - Register
        var email = $"complete{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerReq);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Login
        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = JwtTestHelper.ExtractToken(loginResponse.Content);
        token.Should().NotBeNullOrWhiteSpace();

        // Act - Access protected resource
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var protectedResponse = await _client.GetAsync("/api/requests/mine");
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Logout
        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify - Token still works (JWT is stateless) but user should have logged out
        var afterLogoutResponse = await _client.GetAsync("/api/requests/mine");
        afterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.OK); // Token still valid until expiry
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
        var accessToken = JwtTestHelper.ExtractToken(loginResponse.Content) ?? throw new InvalidOperationException("No token returned");
        var refreshToken = JwtTestHelper.ExtractRefreshToken(loginResponse.Content) ?? throw new InvalidOperationException("No refresh token returned");

        return (accessToken, refreshToken);
    }
}
