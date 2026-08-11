using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.API.Tests.Builders;
using ServiceMarketplace.API.Tests.Fixtures;
using ServiceMarketplace.API.Tests.Helpers;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public class MvpGateAFullTransactionTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MvpGateAFullTransactionTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteServiceTransaction_UsesPublicAndAdminApis_AndRetainsAuditHistory()
    {
        var adminToken = await CreateAdminTokenAsync();

        var customerEmail = $"gatea-customer{Guid.NewGuid():N}@test.com";
        var customerToken = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);

        var providerEmail = $"gatea-provider{Guid.NewGuid():N}@test.com";
        var providerToken = await RegisterAndLoginAsync(providerEmail, RoleConstants.ServiceProvider);

        SetAuthHeader(providerToken);
        var upsertResponse = await _client.PutAsJsonAsync("/api/provider-applications/me", new UpsertProviderApplicationDto
        {
            DisplayName = "Gate A Provider",
            BusinessName = "Gate A Services",
            Skills = "Plumbing, leak repair",
            PrimaryCategory = "Plumbing",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceAreaCity = "Kolkata",
            ServiceAreaState = "West Bengal",
            ServiceAreaZone = "South Kolkata",
            ServiceZoneId = SouthKolkataZoneId,
            HourlyRate = 600,
            IsAvailable = true,
            IdentityVerificationSubmitted = true,
            AddressVerificationSubmitted = true,
            BackgroundCheckConsent = true
        });
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await _client.PostAsync("/api/provider-applications/me/submit", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await ReadJsonAsync<ProviderApplicationDto>(submitResponse);
        submitted.Status.Should().Be(ProviderApplicationStatus.Submitted);

        SetAuthHeader(adminToken);
        var reviewApplicationResponse = await _client.PostAsJsonAsync($"/api/admin/provider-applications/{submitted.Id}/review", new ReviewProviderApplicationDto
        {
            Status = ProviderApplicationStatus.Approved,
            ReviewNotes = "Approved for MVP Gate A smoke test."
        });
        reviewApplicationResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        providerToken = await LoginAsync(providerEmail);
        SetAuthHeader(customerToken);
        var requestResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Gate A plumbing job",
            Description = "End-to-end service transaction smoke test.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "88 Gate A Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Please bring basic repair tools."
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requestPayload = await ReadJsonAsync<CreateServiceRequestResponse>(requestResponse);

        SetAuthHeader(providerToken);
        var availableResponse = await _client.GetAsync("/api/requests/available");
        availableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var available = await ReadJsonAsync<List<ProviderServiceRequestDto>>(availableResponse);
        available.Should().ContainSingle(r => r.Id == requestPayload.RequestId);
        available.Should().OnlyContain(r => !r.IsExactLocationVisible);

        var bidResponse = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestPayload.RequestId,
            Amount = 1100,
            ProposedDateTime = DateTime.UtcNow.AddHours(5),
            EstimatedDurationMinutes = 90,
            Message = "I can complete this today."
        });
        bidResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(customerToken);
        var bidsResponse = await _client.GetAsync($"/api/bids/{requestPayload.RequestId}");
        bidsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bids = await ReadJsonAsync<List<BidDto>>(bidsResponse);
        var bid = bids.Should().ContainSingle().Subject;
        bid.ProviderDisplayName.Should().Be("Gate A Provider");

        var acceptResponse = await _client.PostAsync($"/api/requests/{requestPayload.RequestId}/accept/{bid.Id}", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(providerToken);
        var acceptedDetailsResponse = await _client.GetAsync($"/api/requests/{requestPayload.RequestId}/details");
        acceptedDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptedDetails = await ReadJsonAsync<ProviderServiceRequestDto>(acceptedDetailsResponse);
        acceptedDetails.IsExactLocationVisible.Should().BeTrue();
        acceptedDetails.ExactLocation.Should().Be("88 Gate A Address, Kolkata, West Bengal");

        SetAuthHeader(customerToken);
        var ordersResponse = await _client.GetAsync("/api/orders/customer");
        ordersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await ReadJsonAsync<List<ServiceOrderDto>>(ordersResponse);
        var order = orders.Should().ContainSingle(o => o.ServiceRequestId == requestPayload.RequestId).Subject;

        SetAuthHeader(providerToken);
        (await _client.PostAsync($"/api/orders/{order.Id}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsync($"/api/orders/{order.Id}/provider-complete", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(customerToken);
        var paymentResponse = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.DirectUpi,
            Amount = 1100,
            ReferenceNumber = "GATEA-UPI-001",
            Notes = "Recorded during MVP Gate A smoke test."
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var completeResponse = await _client.PostAsync($"/api/orders/{order.Id}/confirm-complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewResponse = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/review", new CreateServiceOrderReviewDto
        {
            Rating = 5,
            Feedback = "Gate A transaction completed successfully."
        });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await ReadJsonAsync<ServiceOrderReviewDto>(reviewResponse);

        SetAuthHeader(adminToken);
        var moderationResponse = await _client.PostAsJsonAsync($"/api/admin/order-reviews/{review.Id}/moderate", new ModerateServiceOrderReviewDto
        {
            IsHidden = true,
            ModerationNotes = "Hidden during MVP Gate A moderation smoke test."
        });
        moderationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var moderatedReview = await ReadJsonAsync<ServiceOrderReviewDto>(moderationResponse);
        moderatedReview.IsHidden.Should().BeTrue();

        SetAuthHeader(customerToken);
        var auditResponse = await _client.GetAsync($"/api/orders/{order.Id}/audit");
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditEvents = await ReadJsonAsync<List<ServiceOrderAuditEventDto>>(auditResponse);
        auditEvents.Select(e => e.EventType).Should().Contain(new[]
        {
            "ServiceOrderCreated",
            "ServiceOrderStarted",
            "ServiceOrderProviderCompleted",
            "ServiceOrderPaymentRecorded",
            "ServiceOrderCompleted",
            "ServiceOrderReviewCreated",
            "ServiceOrderReviewModerated"
        });
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        var adminEmail = $"gatea-admin{Guid.NewGuid():N}@test.com";
        await RegisterAsync(adminEmail, RoleConstants.User);

        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = adminEmail.Trim().ToUpperInvariant();
        var user = await dbContext.Users.FirstAsync(u => u.NormalizedEmail == normalizedEmail);
        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == RoleConstants.Admin);

        dbContext.UserRoles.Add(new ServiceMarketplace.Domain.Entities.UserRole
        {
            UserId = user.Id,
            RoleId = adminRole.Id
        });
        user.UserType = ServiceMarketplace.Domain.Enums.UserType.Admin;
        user.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return await LoginAsync(adminEmail);
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

    private async Task<string> RegisterAndLoginAsync(string email, string role)
    {
        await RegisterAsync(email, role);
        return await LoginAsync(email);
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
