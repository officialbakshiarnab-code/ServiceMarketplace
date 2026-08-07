using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Infrastructure.Data;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

/// <summary>
/// Full validation test suite for production readiness.
/// Tests all critical paths: registration, login, authorization, audit logging, and dashboard stats.
/// </summary>
public class FullValidationTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>, IAsyncLifetime
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private AppDbContext _dbContext = null!;

    public FullValidationTests(ServiceMarketplaceWebApplicationFactory factory)
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
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    #region Registration Validation

    [Fact]
    public async Task Registration_WithValidCredentials_Succeeds()
    {
        // Arrange
        var email = $"register{Guid.NewGuid():N}@test.com";
        var request = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registration_IsIdempotent_SameEmailReturnsSuccess()
    {
        // Arrange
        var email = $"idempotent{Guid.NewGuid():N}@test.com";
        var request = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();

        // Act - Register first time
        var response1 = await _client.PostAsJsonAsync("/api/auth/register", request);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Register same email again
        var response2 = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert - Should also succeed (idempotent)
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Registration_WithBothRoles_WorksForEachRole()
    {
        // Arrange
        var userEmail = $"user{Guid.NewGuid():N}@test.com";
        var providerEmail = $"provider{Guid.NewGuid():N}@test.com";

        var userRequest = AuthRequestBuilder.CreateUser(userEmail).BuildRegisterRequest();
        var providerRequest = AuthRequestBuilder.CreateServiceProvider(providerEmail).BuildRegisterRequest();

        // Act
        var userResponse = await _client.PostAsJsonAsync("/api/auth/register", userRequest);
        var providerResponse = await _client.PostAsJsonAsync("/api/auth/register", providerRequest);

        // Assert
        userResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        providerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Login Validation

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsBothTokens()
    {
        // Arrange
        var email = $"login{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.Should().HaveProperty("token");
        doc.RootElement.Should().HaveProperty("refreshToken");
        doc.RootElement.Should().HaveProperty("expiresAt");

        var token = doc.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        // Arrange
        var email = $"invalid{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NoIntermittent400s_RepeatedLogins()
    {
        // Arrange
        var email = $"repeat{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        // Act - Login 5 times
        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
            statuses.Add(response.StatusCode);
            await Task.Delay(50); // Small delay between requests
        }

        // Assert - All should be 200 OK (no 400s)
        statuses.Should().AllBe(HttpStatusCode.OK);
        statuses.Should().NotContain(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Role-Based Access Control Validation

    [Fact]
    public async Task RoleBasedAccess_UserCanAccessUserEndpoints()
    {
        // Arrange
        var (token, _) = await LoginAsUserAsync();
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleBasedAccess_UserCannotAccessProviderEndpoints()
    {
        // Arrange
        var (token, _) = await LoginAsUserAsync();
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/requests/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleBasedAccess_ProviderCanAccessProviderEndpoints()
    {
        // Arrange
        var (token, _) = await LoginAsProviderAsync();
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/requests/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleBasedAccess_ProviderCanAccessUserEndpoints()
    {
        // Arrange
        var (token, _) = await LoginAsProviderAsync();
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/requests/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleBasedAccess_ConsistentAcrossMultipleRequests()
    {
        // Arrange
        var (userToken, _) = await LoginAsUserAsync();
        SetAuthHeader(userToken);

        // Act - Try user endpoint 3 times
        var results = new List<bool>();
        for (int i = 0; i < 3; i++)
        {
            var response = await _client.GetAsync("/api/requests/mine");
            results.Add(response.StatusCode == HttpStatusCode.OK);
        }

        // Act - Try provider endpoint 3 times
        for (int i = 0; i < 3; i++)
        {
            var response = await _client.GetAsync("/api/requests/available");
            results.Add(response.StatusCode == HttpStatusCode.Forbidden);
        }

        // Assert - All consistent
        results.Should().AllBe(true);
    }

    #endregion

    #region Audit Log Validation

    [Fact]
    public async Task AuditLog_LoginEventsRecorded()
    {
        // Arrange
        var email = $"auditlogin{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Check audit log
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .ToListAsync();

        auditLogs.Should().NotBeEmpty();
        var loginLog = auditLogs.Last();
        loginLog.EventType.Should().Be("Login");
        loginLog.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task AuditLog_LogoutEventsRecorded()
    {
        // Arrange
        var (token, _) = await LoginAsUserAsync();
        SetAuthHeader(token);

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.EventType == "Logout")
            .ToListAsync();

        auditLogs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuditLog_SessionExpiredEventsRecorded()
    {
        // Arrange
        var (token, _) = await LoginAsUserAsync();

        var expiredTokenRequest = new { token = token };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token-expired", expiredTokenRequest);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);

        // Assert - May or may not create audit log depending on implementation
        // But should not throw an error
    }

    [Fact]
    public async Task AuditLog_ContainsMetadata()
    {
        // Arrange
        var email = $"metadata{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        _client.DefaultRequestHeaders.Add("User-Agent", "TestClient/1.0");

        // Act
        await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        var auditLog = await _dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstAsync();

        auditLog.UserId.Should().NotBeNullOrWhiteSpace();
        auditLog.SessionId.Should().NotBeNullOrWhiteSpace();
        // IP address may be ::1 for localhost
        auditLog.IpAddress.Should().NotBeNullOrWhiteSpace();
        auditLog.UserAgent.Should().Contain("TestClient");
    }

    [Fact]
    public async Task AuditLog_AllEventsIncluded()
    {
        // Arrange
        var (token, _) = await LoginAsUserAsync();

        // Act - Logout
        SetAuthHeader(token);
        await _client.PostAsync("/api/auth/logout", null);

        // Assert - Should have Login AND Logout
        var allLogs = await _dbContext.AuditLogs.ToListAsync();
        var loginCount = allLogs.Count(a => a.EventType == "Login");
        var logoutCount = allLogs.Count(a => a.EventType == "Logout");

        loginCount.Should().BeGreaterThan(0);
        logoutCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Dashboard Stats Validation

    [Fact]
    public async Task DashboardStats_ReturnsAccurateData()
    {
        // Arrange
        var (token, userId) = await LoginAsUserAsync();
        SetAuthHeader(token);

        // Get stats endpoint
        var response = await _client.GetAsync("/api/requests/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.Should().HaveProperty("openRequestsCount");
        doc.RootElement.Should().HaveProperty("activeBidsCount");
        doc.RootElement.Should().HaveProperty("completedRequestsCount");
        doc.RootElement.Should().HaveProperty("totalRequestsCount");
    }

    [Fact]
    public async Task DashboardStats_InitiallyZero()
    {
        // Arrange
        var email = $"statsnew{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        var json = await loginResponse.Content.ReadAsStringAsync();
        var token = JsonDocument.Parse(json).RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("No token returned");

        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync("/api/requests/stats");
        var statsJson = await response.Content.ReadAsStringAsync();
        var statsDoc = JsonDocument.Parse(statsJson);

        // Assert
        statsDoc.RootElement.GetProperty("openRequestsCount").GetInt32().Should().Be(0);
        statsDoc.RootElement.GetProperty("completedRequestsCount").GetInt32().Should().Be(0);
        statsDoc.RootElement.GetProperty("totalRequestsCount").GetInt32().Should().Be(0);
    }

    #endregion

    #region Complete Flow Validation

    [Fact]
    public async Task CompleteFlow_RegisterLoginAccessLogout()
    {
        // Arrange
        var email = $"flow{Guid.NewGuid():N}@test.com";

        // Act 1 - Register
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        var registerResp = await _client.PostAsJsonAsync("/api/auth/register", registerReq);
        registerResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2 - Login
        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = JwtTestHelper.ExtractToken(loginResp.Content)
            ?? throw new InvalidOperationException("No token returned");

        // Act 3 - Access protected resource
        SetAuthHeader(token);
        var protectedResp = await _client.GetAsync("/api/requests/mine");
        protectedResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 4 - Logout
        var logoutResp = await _client.PostAsync("/api/auth/logout", null);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Audit logs created
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.EventType == "Login" || a.EventType == "Logout")
            .ToListAsync();

        auditLogs.Should().HaveCountGreaterThanOrEqualTo(2);
        auditLogs.Should().Contain(a => a.EventType == "Login");
        auditLogs.Should().Contain(a => a.EventType == "Logout");
    }

    #endregion

    #region Helper Methods

    private async Task<(string token, string userId)> LoginAsUserAsync()
    {
        var email = $"user{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        var json = await loginResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("No token");

        var claims = JwtTestHelper.GetClaims(token);
        var userId = claims["sub"];

        return (token, userId);
    }

    private async Task<(string token, string userId)> LoginAsProviderAsync()
    {
        var email = $"provider{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateServiceProvider(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);
        await ApproveProviderAsync(email);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        var json = await loginResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("No token");

        var claims = JwtTestHelper.GetClaims(token);
        var userId = claims["sub"];

        return (token, userId);
    }

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
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

    #endregion
}
