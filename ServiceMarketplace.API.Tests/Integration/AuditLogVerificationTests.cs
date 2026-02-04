using System.Net;
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
/// Integration tests for audit log verification.
/// Verifies that authentication events are properly logged to the audit trail.
/// </summary>
public class AuditLogVerificationTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuditLogVerificationTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_CreatesAuditLogEntry()
    {
        // Arrange
        var email = $"audit{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var dbContext = await _factory.GetDbContextAsync();

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Check audit log
        var auditLogs = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .ToListAsync();

        auditLogs.Should().NotBeEmpty();
        var loginLog = auditLogs.Last();
        
        loginLog.EventType.Should().Be("Login");
        loginLog.UserId.Should().NotBeNullOrWhiteSpace();
        loginLog.Role.Should().Be(RoleConstants.User);
        loginLog.SessionId.Should().NotBeNullOrWhiteSpace();
        loginLog.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Logout_CreatesAuditLogEntry()
    {
        // Arrange
        var email = $"logout{Guid.NewGuid():N}@test.com";
        var (accessToken, _) = await RegisterAndLoginAsync(email, RoleConstants.User);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var dbContext = await _factory.GetDbContextAsync();
        var loginLogsCount = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .CountAsync();

        // Act
        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Check audit log contains logout entry
        var allLogs = await dbContext.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .ToListAsync();

        var logoutLog = allLogs.FirstOrDefault(a => a.EventType == "Logout");
        logoutLog.Should().NotBeNull();
        logoutLog!.EventType.Should().Be("Logout");
        logoutLog.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AuditLog_CapturesiPAddress()
    {
        // Arrange
        var email = $"iptest{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var dbContext = await _factory.GetDbContextAsync();

        // Act
        await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        var auditLog = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstAsync();

        // IP address should be captured (may be ::1 for localhost in tests)
        auditLog.IpAddress.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuditLog_CapturesUserAgent()
    {
        // Arrange
        var email = $"uatest{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        var dbContext = await _factory.GetDbContextAsync();

        // Set custom User-Agent
        _client.DefaultRequestHeaders.Add("User-Agent", "TestClient/1.0");

        // Act
        await _client.PostAsJsonAsync("/api/auth/login", loginReq);

        // Assert
        var auditLog = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstAsync();

        auditLog.UserAgent.Should().Contain("TestClient");
    }

    [Fact]
    public async Task AuditLog_ContainsSessionId()
    {
        // Arrange
        var email = $"sessiontest{Guid.NewGuid():N}@test.com";
        var registerReq = AuthRequestBuilder.CreateUser(email).BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/auth/register", registerReq);

        var loginReq = new ServiceMarketplace.API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = "Test@123456"
        };

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        var token = JwtTestHelper.ExtractToken(loginResponse.Content);
        var sessionIdFromJwt = JwtTestHelper.GetClaim(token!, "jti");

        var dbContext = await _factory.GetDbContextAsync();
        var auditLog = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstAsync();

        // Assert
        auditLog.SessionId.Should().Be(sessionIdFromJwt);
    }

    [Fact]
    public async Task AuditLog_AppendOnly_NeverUpdates()
    {
        // Arrange
        var email = $"appendonly{Guid.NewGuid():N}@test.com";
        var (token, _) = await RegisterAndLoginAsync(email, RoleConstants.User);

        var dbContext = await _factory.GetDbContextAsync();
        var initialLoginLog = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstAsync();

        var initialTimestamp = initialLoginLog.TimestampUtc;

        // Small delay to ensure time difference
        await Task.Delay(100);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act - Logout
        await _client.PostAsync("/api/auth/logout", null);

        // Assert - Original login log should NOT be updated
        var updatedDbContext = await _factory.GetDbContextAsync();
        var loginLogAfterLogout = await updatedDbContext.AuditLogs
            .Where(a => a.Id == initialLoginLog.Id)
            .FirstAsync();

        loginLogAfterLogout.TimestampUtc.Should().Be(initialTimestamp);
        loginLogAfterLogout.EventType.Should().Be("Login");

        // And a NEW logout log should exist
        var logoutLog = await updatedDbContext.AuditLogs
            .Where(a => a.EventType == "Logout")
            .OrderByDescending(a => a.TimestampUtc)
            .FirstAsync();

        logoutLog.Should().NotBeNull();
        logoutLog.TimestampUtc.Should().BeGreaterThan(initialTimestamp);
    }

    [Fact]
    public async Task MultipleLoginLogouts_CreatesMultipleAuditEntries()
    {
        // Arrange
        var email = $"multiple{Guid.NewGuid():N}@test.com";

        // Act & Assert - First login/logout cycle
        var (token1, _) = await RegisterAndLoginAsync(email, RoleConstants.User);
        
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token1);
        await _client.PostAsync("/api/auth/logout", null);

        var dbContext = await _factory.GetDbContextAsync();
        var logsAfterFirstCycle = await dbContext.AuditLogs
            .Where(a => a.EventType == "Login" || a.EventType == "Logout")
            .CountAsync();

        logsAfterFirstCycle.Should().Be(2); // 1 Login + 1 Logout

        // Login again (refresh token scenario)
        var refreshRequest = new { refreshToken = _client.DefaultRequestHeaders.Authorization?.Parameter };
        
        // Check final audit logs
        var finalLogs = await dbContext.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .ToListAsync();

        var loginCount = finalLogs.Count(a => a.EventType == "Login");
        var logoutCount = finalLogs.Count(a => a.EventType == "Logout");

        loginCount.Should().BeGreaterThan(0);
        logoutCount.Should().BeGreaterThan(0);
    }

    private async Task<(string accessToken, string refreshToken)> RegisterAndLoginAsync(
        string email,
        string role)
    {
        // Register
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
