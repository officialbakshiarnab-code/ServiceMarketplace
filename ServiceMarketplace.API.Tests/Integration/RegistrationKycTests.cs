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
    public async Task Register_WithoutPhoneNumber_ReturnsBadRequest()
    {
        var request = CreateRegisterRequest("missing-phone", "User");
        request.PhoneNumber = string.Empty;

        using var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Phone number is required");
    }

    [Fact]
    public async Task Register_WithDuplicatePhoneNumber_ReturnsBadRequest()
    {
        var phoneNumber = "9876543210";
        var first = CreateRegisterRequest("phone-one", "User");
        first.PhoneNumber = phoneNumber;
        var second = CreateRegisterRequest("phone-two", "User");
        second.PhoneNumber = phoneNumber;

        using var firstResponse = await _client.PostAsJsonAsync("/api/auth/register", first);
        using var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", second);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await secondResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Phone number is already registered");
    }

    [Fact]
    public async Task Register_WithProviderCommercialDetails_CreatesSubmittedProviderApplication()
    {
        var request = CreateRegisterRequest("commercial-provider", "ServiceProvider");
        request.CommercialOnboarding = new RegistrationCommercialOnboardingRequest
        {
            WantsProvider = true,
            Business = new RegistrationBusinessDetailsRequest
            {
                LegalName = "North Kolkata Repairs Private Limited",
                TradingName = "North Kolkata Repairs",
                BusinessType = "Company",
                WebsiteOrDomain = "northkolkatarepairs.test",
                RequestedSeatLimit = 5
            },
            Provider = new RegistrationProviderDetailsRequest
            {
                ProviderType = "Business",
                Skills = "AC repair, refrigerator repair",
                Profession = "Appliance service company",
                YearsOfExperience = 8,
                PrimaryCategory = "Home Appliance Repair",
                ServiceAreaCity = "Kolkata",
                ServiceAreaState = "West Bengal",
                ServiceAreaZone = "North Kolkata",
                PricingType = "StartingPrice",
                Rate = 699,
                Availability = "Mon-Sat",
                Languages = "Bengali, Hindi, English"
            }
        };

        using var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbContext = await _factory.GetDbContextAsync();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == request.Email.ToUpperInvariant());
        user.PhoneNumber.Should().Be(request.PhoneNumber);

        var profile = await dbContext.ServiceProviderProfiles.AsNoTracking().SingleAsync(p => p.UserId == user.Id);
        profile.Status.Should().Be(ProviderApplicationStatus.Submitted);
        profile.BusinessName.Should().Be("North Kolkata Repairs");
        profile.Skills.Should().Be("AC repair, refrigerator repair");
        profile.PrimaryCategory.Should().Be("Home Appliance Repair");
        profile.HourlyRate.Should().Be(699);
        profile.SubmittedAt.Should().NotBeNull();
        profile.Bio.Should().Contain("Requested seats: 5");
    }

    [Fact]
    public async Task Register_WithSellerCommercialDetails_CreatesSubmittedSellerApplication()
    {
        var request = CreateRegisterRequest("commercial-seller", "User");
        request.CommercialOnboarding = new RegistrationCommercialOnboardingRequest
        {
            WantsSeller = true,
            Business = new RegistrationBusinessDetailsRequest
            {
                TradingName = "Garia Home Goods",
                BusinessType = "Shop",
                RequestedSeatLimit = 10
            },
            Seller = new RegistrationSellerDetailsRequest
            {
                StoreName = "Garia Home Goods",
                BusinessName = "Garia Home Goods",
                Gstin = string.Empty,
                ProductCategories = "Home improvement, fixtures",
                ProductConditionFocus = "Both",
                PickupAddress = "45 Garia Station Road",
                City = "Kolkata",
                State = "West Bengal",
                Description = "Local shop with pickup and delivery."
            }
        };

        using var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbContext = await _factory.GetDbContextAsync();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == request.Email.ToUpperInvariant());

        var profile = await dbContext.SellerProfiles.AsNoTracking().SingleAsync(p => p.UserId == user.Id);
        profile.Status.Should().Be(SellerApplicationStatus.Submitted);
        profile.StoreName.Should().Be("Garia Home Goods");
        profile.Gstin.Should().BeNull();
        profile.PickupAddress.Should().Be("45 Garia Station Road");
        profile.SubmittedAt.Should().NotBeNull();
        profile.Description.Should().Contain("Requested seats: 10");
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
            Role = role,
            PhoneNumber = CreatePhoneNumber(prefix)
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
            { new StringContent(role), nameof(RegisterRequest.Role) },
            { new StringContent(CreatePhoneNumber(email)), nameof(RegisterRequest.PhoneNumber) }
        };
    }

    private static string CreateEmail(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}@test.com";
    }

    private static string CreatePhoneNumber(string value)
    {
        var digits = value.Where(char.IsLetterOrDigit).Select(c => (int)c).Sum();
        return $"98{digits % 10_000_000:0000000}1";
    }
}
