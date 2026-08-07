using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class Phase4MatchingAndBiddingTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid ElectricalCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    private static readonly Guid NorthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase4MatchingAndBiddingTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AvailableRequests_AreFilteredByApprovedProviderCategoryAndZoneCoverage()
    {
        var matchingRequestId = await CreateRequestAsync(
            $"phase4-match-customer{Guid.NewGuid():N}@test.com",
            "South plumbing work",
            PlumbingCategoryId,
            SouthKolkataZoneId,
            "10 Exact South Road, Kolkata, West Bengal");

        await CreateRequestAsync(
            $"phase4-electrical-customer{Guid.NewGuid():N}@test.com",
            "South electrical work",
            ElectricalCategoryId,
            SouthKolkataZoneId,
            "11 Exact South Road, Kolkata, West Bengal");

        await CreateRequestAsync(
            $"phase4-north-customer{Guid.NewGuid():N}@test.com",
            "North plumbing work",
            PlumbingCategoryId,
            NorthKolkataZoneId,
            "12 Exact North Road, Kolkata, West Bengal");

        var providerEmail = $"phase4-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail, PlumbingCategoryId, SouthKolkataZoneId, "Coverage Provider", 500);
        SetAuthHeader(await LoginAsync(providerEmail));

        var response = await _client.GetAsync("/api/requests/available");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var available = await ReadJsonAsync<List<ProviderServiceRequestDto>>(response);
        available.Should().ContainSingle(r => r.Id == matchingRequestId);
        available.Should().NotContain(r => r.Category == "Electrical");
        available.Should().OnlyContain(r => r.ServiceZoneId == SouthKolkataZoneId);
    }

    [Fact]
    public async Task PlaceBid_WhenProviderCoverageDoesNotMatch_ReturnsBadRequest()
    {
        var requestId = await CreateRequestAsync(
            $"phase4-mismatch-customer{Guid.NewGuid():N}@test.com",
            "Electrical repair",
            ElectricalCategoryId,
            SouthKolkataZoneId,
            "20 Exact South Road, Kolkata, West Bengal");

        var providerEmail = $"phase4-mismatch-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail, PlumbingCategoryId, SouthKolkataZoneId, "Plumbing Only", 450);
        SetAuthHeader(await LoginAsync(providerEmail));

        var response = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestId,
            Amount = 1000,
            ProposedDateTime = DateTime.UtcNow.AddHours(3),
            EstimatedDurationMinutes = 90,
            Message = "I can take this."
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CustomerBidList_ReturnsProviderComparisonDetailsAndRanking()
    {
        var customerEmail = $"phase4-bids-customer{Guid.NewGuid():N}@test.com";
        var requestId = await CreateRequestAsync(
            customerEmail,
            "Compare plumbing bids",
            PlumbingCategoryId,
            SouthKolkataZoneId,
            "30 Exact South Road, Kolkata, West Bengal");

        await PlaceBidAsProviderAsync($"phase4-rank-a{Guid.NewGuid():N}@test.com", "Higher Bidder", 900, 600, 90, requestId);
        await PlaceBidAsProviderAsync($"phase4-rank-b{Guid.NewGuid():N}@test.com", "Lower Bidder", 700, 400, 60, requestId);

        SetAuthHeader(await LoginAsync(customerEmail));
        var response = await _client.GetAsync($"/api/bids/{requestId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var bids = await ReadJsonAsync<List<BidDto>>(response);
        bids.Should().HaveCount(2);
        bids[0].ProviderDisplayName.Should().Be("Lower Bidder");
        bids[0].ComparisonRank.Should().Be(1);
        bids[0].ProviderHourlyRate.Should().Be(400);
        bids[0].EstimatedDurationMinutes.Should().Be(60);
        bids[1].ComparisonRank.Should().Be(2);
    }

    [Fact]
    public async Task AcceptedProviderCanViewExactLocation_AfterCustomerAcceptsBid()
    {
        var customerEmail = $"phase4-accept-customer{Guid.NewGuid():N}@test.com";
        var requestId = await CreateRequestAsync(
            customerEmail,
            "Accepted location work",
            PlumbingCategoryId,
            SouthKolkataZoneId,
            "40 Private Accepted Address, Kolkata, West Bengal");

        var providerToken = await PlaceBidAsProviderAsync(
            $"phase4-accept-provider{Guid.NewGuid():N}@test.com",
            "Accepted Provider",
            800,
            500,
            120,
            requestId);

        SetAuthHeader(await LoginAsync(customerEmail));
        var bidsResponse = await _client.GetAsync($"/api/bids/{requestId}");
        var bids = await ReadJsonAsync<List<BidDto>>(bidsResponse);
        var bidId = bids.Should().ContainSingle().Subject.Id;

        var acceptResponse = await _client.PostAsync($"/api/requests/{requestId}/accept/{bidId}", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(providerToken);
        var detailResponse = await _client.GetAsync($"/api/requests/{requestId}/details");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await ReadJsonAsync<ProviderServiceRequestDto>(detailResponse);
        detail.IsExactLocationVisible.Should().BeTrue();
        detail.ExactLocation.Should().Be("40 Private Accepted Address, Kolkata, West Bengal");
        detail.Latitude.Should().Be(22.501);
        detail.Longitude.Should().Be(88.361);
    }

    private async Task<Guid> CreateRequestAsync(
        string customerEmail,
        string title,
        Guid categoryId,
        Guid zoneId,
        string location)
    {
        var token = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);
        SetAuthHeader(token);

        var response = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = title,
            Description = "Phase 4 test request.",
            ServiceCategoryId = categoryId,
            ServiceZoneId = zoneId,
            Location = location,
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Use Phase 4 matching."
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ReadJsonAsync<CreateServiceRequestResponse>(response);
        return payload.RequestId;
    }

    private async Task<string> PlaceBidAsProviderAsync(
        string providerEmail,
        string displayName,
        decimal amount,
        decimal hourlyRate,
        int durationMinutes,
        Guid requestId)
    {
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail, PlumbingCategoryId, SouthKolkataZoneId, displayName, hourlyRate);
        var token = await LoginAsync(providerEmail);
        SetAuthHeader(token);

        var response = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestId,
            Amount = amount,
            ProposedDateTime = DateTime.UtcNow.AddHours(4),
            EstimatedDurationMinutes = durationMinutes,
            Message = $"Bid from {displayName}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return token;
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

    private async Task ApproveProviderAsync(
        string email,
        Guid categoryId,
        Guid zoneId,
        string displayName,
        decimal hourlyRate)
    {
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(u => u.ServiceProviderProfile)
            .FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        var profile = user.ServiceProviderProfile
            ?? throw new InvalidOperationException("Provider profile was not created.");

        profile.DisplayName = displayName;
        profile.BusinessName = $"{displayName} Services";
        profile.Skills = "Plumbing, repairs";
        profile.PrimaryCategory = "Plumbing";
        profile.ServiceCategoryId = categoryId;
        profile.ServiceAreaCity = "Kolkata";
        profile.ServiceAreaState = "West Bengal";
        profile.ServiceAreaZone = "South Kolkata";
        profile.ServiceZoneId = zoneId;
        profile.HourlyRate = hourlyRate;
        profile.IsAvailable = true;
        profile.IdentityVerificationSubmitted = true;
        profile.AddressVerificationSubmitted = true;
        profile.BackgroundCheckConsent = true;
        profile.Status = ProviderApplicationStatus.Approved;

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

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Response body could not be deserialized.");
    }

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}
