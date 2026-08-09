using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Models.Auth;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.Domain.Enums;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class RegistrationKycTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RegistrationKycTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_JsonUserRegistration_StillSucceeds()
    {
        var request = CreateRegisterRequest("json-user", "User");

        using var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_MultipartProviderRegistration_WithoutFile_StillSucceeds()
    {
        var email = CreateEmail("multipart-provider-no-file");
        using var content = CreateMultipartContent(email, "ServiceProvider");

        using var response = await _client.PostAsync("/api/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbContext = await _factory.GetDbContextAsync();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.UserType.Should().Be(UserType.Provider);
        user.IsKycSubmitted.Should().BeFalse();
    }

    [Fact]
    public async Task Register_MultipartProviderRegistration_WithValidDocument_MarksKycSubmitted()
    {
        var email = CreateEmail("multipart-provider-kyc");
        using var content = CreateMultipartContent(email, "ServiceProvider");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "GovernmentIdImage", "identity.pdf");

        using var response = await _client.PostAsync("/api/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbContext = await _factory.GetDbContextAsync();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsKycSubmitted.Should().BeTrue();
        user.GovIdFilePath.Should().NotBeNullOrWhiteSpace();

        var profile = await dbContext.ServiceProviderProfiles.AsNoTracking().SingleAsync(p => p.UserId == user.Id);
        profile.IdentityVerificationSubmitted.Should().BeTrue();
    }

    [Fact]
    public async Task Register_MultipartProviderRegistration_WithInvalidDocument_DoesNotCrash()
    {
        var email = CreateEmail("multipart-provider-invalid-kyc");
        using var content = CreateMultipartContent(email, "ServiceProvider");
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "GovernmentIdImage", "identity.exe");

        using var response = await _client.PostAsync("/api/auth/register", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbContext = await _factory.GetDbContextAsync();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsKycSubmitted.Should().BeFalse();
        user.GovIdFilePath.Should().BeNull();
    }

    private static RegisterRequest CreateRegisterRequest(string prefix, string role)
    {
        return new RegisterRequest
        {
            Email = CreateEmail(prefix),
            Password = "Demo@12345",
            FirstName = "Demo",
            LastName = "User",
            DateOfBirth = DateTime.Today.AddYears(-25),
            Role = role
        };
    }

    private static MultipartFormDataContent CreateMultipartContent(string email, string role)
    {
        return new MultipartFormDataContent
        {
            { new StringContent(email), nameof(RegisterRequest.Email) },
            { new StringContent("Demo@12345"), nameof(RegisterRequest.Password) },
            { new StringContent("Demo"), nameof(RegisterRequest.FirstName) },
            { new StringContent("Provider"), nameof(RegisterRequest.LastName) },
            { new StringContent(DateTime.Today.AddYears(-25).ToString("yyyy-MM-dd")), nameof(RegisterRequest.DateOfBirth) },
            { new StringContent(role), nameof(RegisterRequest.Role) }
        };
    }

    private static string CreateEmail(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}@test.com";
    }
}
