using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public sealed class Phase27AdminProvisioningTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>, IAsyncLifetime
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private AppDbContext _dbContext = null!;

    public Phase27AdminProvisioningTests(ServiceMarketplaceWebApplicationFactory factory)
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
    public async Task Bootstrap_CreatesFirstAdmin_AndAdminTokenCanUseAdminApi()
    {
        var email = $"bootstrap-admin{Guid.NewGuid():N}@test.com";

        var result = await ProvisionAsync(email, resetPassword: false);

        result.Success.Should().BeTrue(result.Message);
        result.CreatedUser.Should().BeTrue();
        result.GrantedAdminRole.Should().BeTrue();
        result.PasswordReset.Should().BeFalse();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.NormalizedEmail == email.ToUpperInvariant());

        user.UserType.Should().Be(UserType.Admin);
        user.UserRoles.Select(ur => ur.Role.Name).Should().Contain(RoleConstants.Admin);

        var token = await LoginAsync(email, "Admin@Test123");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims
            .Where(c => c.Type == AdministrativePermissionConstants.ClaimType)
            .Select(c => c.Value)
            .Should()
            .Contain(AdministrativePermissionConstants.PlatformAdmin);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var adminResponse = await _client.GetAsync("/api/admin/kpis");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auditEvents = await _dbContext.AuditLogs
            .Where(a => a.UserId == user.Id.ToString())
            .Select(a => a.EventType)
            .ToListAsync();
        auditEvents.Should().Contain("AdminProvisioned");
    }

    [Fact]
    public async Task Bootstrap_GrantsAdminRoleToExistingUser_WithoutPasswordReset()
    {
        var email = $"existing-admin{Guid.NewGuid():N}@test.com";
        await RegisterUserAsync(email);

        var result = await ProvisionAsync(email, resetPassword: false);

        result.Success.Should().BeTrue(result.Message);
        result.CreatedUser.Should().BeFalse();
        result.GrantedAdminRole.Should().BeTrue();
        result.PasswordReset.Should().BeFalse();

        var oldPasswordToken = await LoginAsync(email, "Test@123456");
        oldPasswordToken.Should().NotBeNullOrWhiteSpace();

        var user = await _dbContext.Users.FirstAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        var auditEvents = await _dbContext.AuditLogs
            .Where(a => a.UserId == user.Id.ToString())
            .Select(a => a.EventType)
            .ToListAsync();
        auditEvents.Should().Contain("AdminRoleGranted");
    }

    [Fact]
    public async Task Bootstrap_DuplicateIsIdempotent_AndPasswordResetRequiresExplicitFlag()
    {
        var email = $"idempotent-admin{Guid.NewGuid():N}@test.com";
        var first = await ProvisionAsync(email, resetPassword: false);
        first.Success.Should().BeTrue(first.Message);

        var duplicate = await ProvisionAsync(email, resetPassword: false, password: "Changed@Test123");
        duplicate.Success.Should().BeTrue(duplicate.Message);
        duplicate.CreatedUser.Should().BeFalse();
        duplicate.GrantedAdminRole.Should().BeFalse();
        duplicate.PasswordReset.Should().BeFalse();

        var originalPasswordToken = await LoginAsync(email, "Admin@Test123");
        originalPasswordToken.Should().NotBeNullOrWhiteSpace();

        var reset = await ProvisionAsync(email, resetPassword: true, password: "Changed@Test123");
        reset.Success.Should().BeTrue(reset.Message);
        reset.PasswordReset.Should().BeTrue();

        var resetToken = await LoginAsync(email, "Changed@Test123");
        resetToken.Should().NotBeNullOrWhiteSpace();

        var user = await _dbContext.Users.FirstAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        var resetLogged = await _dbContext.AuditLogs
            .AnyAsync(a => a.UserId == user.Id.ToString() && a.EventType == "AdminPasswordReset");
        resetLogged.Should().BeTrue();
    }

    private async Task<AdminProvisioningResult> ProvisionAsync(
        string email,
        bool resetPassword,
        string password = "Admin@Test123")
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminProvisioningService>();

        return await service.ProvisionAsync(new AdminProvisioningRequest
        {
            Email = email,
            FirstName = "Admin",
            LastName = "Operator",
            PhoneNumber = UniquePhone(),
            Password = password,
            Reason = "Integration test bootstrap",
            ResetPassword = resetPassword
        });
    }

    private async Task RegisterUserAsync(string email)
    {
        var request = AuthRequestBuilder.CreateDefault()
            .WithEmail(email)
            .WithRole(RoleConstants.User)
            .BuildRegisterRequest();

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new API.Models.Auth.LoginRequest
        {
            Email = email,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return JwtTestHelper.ExtractToken(loginResponse.Content)
            ?? throw new InvalidOperationException("No access token returned");
    }

    private static string UniquePhone()
    {
        var suffix = Math.Abs(Guid.NewGuid().GetHashCode()).ToString("D10")[..10];
        return suffix[0] == '0' ? $"9{suffix[1..]}" : suffix;
    }
}
