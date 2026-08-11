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

public class Phase7PaymentsAndReviewsTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase7PaymentsAndReviewsTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CustomerCompletion_RequiresRecordedPayment_AndReleasesPayment()
    {
        var setup = await CreateProviderCompletedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var blockedCompletion = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        blockedCompletion.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var paymentResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.DirectUpi,
            Amount = setup.AgreedAmount,
            ReferenceNumber = "UPI-PHASE7-001"
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await ReadJsonAsync<ServiceOrderPaymentDto>(paymentResponse);
        payment.Status.Should().Be(PaymentStatus.Held);

        var confirmResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await ReadJsonAsync<ServiceOrderDto>(confirmResponse);
        completed.Status.Should().Be(ServiceOrderStatus.Completed);

        await using var dbContext = await _factory.GetDbContextAsync();
        var persistedPayment = await dbContext.ServiceOrderPayments.SingleAsync(p => p.ServiceOrderId == setup.OrderId);
        persistedPayment.Status.Should().Be(PaymentStatus.Released);
        persistedPayment.ReleasedAt.Should().NotBeNull();

        SetAuthHeader(setup.ProviderToken);
        var notificationsResponse = await _client.GetAsync("/api/notifications");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await ReadJsonAsync<List<UserNotificationDto>>(notificationsResponse);
        notifications.Should().Contain(n =>
            n.Type == UserNotificationType.ServiceOrderPaymentRecorded &&
            n.ServiceOrderId == setup.OrderId);
    }

    [Fact]
    public async Task CompletedOrder_CanBeReviewedOnce_AndUpdatesProviderAggregate()
    {
        var setup = await CreateCompletedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var reviewResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/review", new CreateServiceOrderReviewDto
        {
            Rating = 5,
            Feedback = "Excellent work and clear communication."
        });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await ReadJsonAsync<ServiceOrderReviewDto>(reviewResponse);
        review.Rating.Should().Be(5);
        review.ProviderId.Should().Be(setup.ProviderId);

        var duplicateResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/review", new CreateServiceOrderReviewDto
        {
            Rating = 4
        });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var dbContext = await _factory.GetDbContextAsync();
        var providerGuid = Guid.Parse(setup.ProviderId);
        var profile = await dbContext.ServiceProviderProfiles.SingleAsync(p => p.UserId == providerGuid);
        profile.AverageRating.Should().Be(5m);
        profile.ReviewCount.Should().Be(1);

        SetAuthHeader(setup.ProviderToken);
        var notificationsResponse = await _client.GetAsync("/api/notifications");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await ReadJsonAsync<List<UserNotificationDto>>(notificationsResponse);
        notifications.Should().Contain(n =>
            n.Type == UserNotificationType.ServiceOrderReviewReceived &&
            n.ServiceOrderId == setup.OrderId);
    }

    [Fact]
    public async Task PaymentRecording_RejectsPlatformMethodAndNonParticipants()
    {
        var setup = await CreateProviderCompletedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var platformResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.Platform,
            Amount = setup.AgreedAmount
        });
        platformResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var strangerToken = await RegisterAndLoginAsync($"phase7-stranger{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(strangerToken);
        var strangerResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.Cash,
            Amount = setup.AgreedAmount
        });
        strangerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<AcceptedOrderSetup> CreateCompletedOrderAsync()
    {
        var setup = await CreateProviderCompletedOrderAsync();
        SetAuthHeader(setup.CustomerToken);

        var paymentResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.Cash,
            Amount = setup.AgreedAmount,
            Notes = "Paid in cash."
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return setup;
    }

    private async Task<AcceptedOrderSetup> CreateProviderCompletedOrderAsync()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.ProviderToken);
        var startResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerCompleteResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/provider-complete", null);
        providerCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return setup;
    }

    private async Task<AcceptedOrderSetup> CreateAcceptedOrderAsync()
    {
        const decimal agreedAmount = 950m;
        var customerEmail = $"phase7-customer{Guid.NewGuid():N}@test.com";
        var customerToken = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);
        SetAuthHeader(customerToken);

        var requestResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Phase 7 payment review request",
            Description = "Create a service order with payment and review coverage.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "77 Phase Seven Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Phase 7 test."
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requestPayload = await ReadJsonAsync<CreateServiceRequestResponse>(requestResponse);

        var providerEmail = $"phase7-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        var providerToken = await LoginAsync(providerEmail);
        SetAuthHeader(providerToken);

        var bidResponse = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestPayload.RequestId,
            Amount = agreedAmount,
            ProposedDateTime = DateTime.UtcNow.AddHours(4),
            EstimatedDurationMinutes = 75,
            Message = "Phase 7 provider bid."
        });
        bidResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(customerToken);
        var bidsResponse = await _client.GetAsync($"/api/bids/{requestPayload.RequestId}");
        bidsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bids = await ReadJsonAsync<List<BidDto>>(bidsResponse);
        var bidId = bids.Should().ContainSingle().Subject.Id;

        var acceptResponse = await _client.PostAsync($"/api/requests/{requestPayload.RequestId}/accept/{bidId}", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ordersResponse = await _client.GetAsync("/api/orders/customer");
        ordersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await ReadJsonAsync<List<ServiceOrderDto>>(ordersResponse);
        var order = orders.Should().ContainSingle(o => o.ServiceRequestId == requestPayload.RequestId).Subject;

        return new AcceptedOrderSetup(
            customerToken,
            providerToken,
            requestPayload.RequestId,
            bidId,
            order.Id,
            order.CustomerId,
            order.ProviderId,
            agreedAmount);
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

    private async Task ApproveProviderAsync(string email)
    {
        await using var dbContext = await _factory.GetDbContextAsync();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(u => u.ServiceProviderProfile)
            .FirstAsync(u => u.NormalizedEmail == normalizedEmail);

        var profile = user.ServiceProviderProfile
            ?? throw new InvalidOperationException("Provider profile was not created.");

        profile.DisplayName = "Phase 7 Provider";
        profile.BusinessName = "Phase 7 Services";
        profile.Skills = "Plumbing, repairs";
        profile.PrimaryCategory = "Plumbing";
        profile.ServiceCategoryId = PlumbingCategoryId;
        profile.ServiceAreaCity = "Kolkata";
        profile.ServiceAreaState = "West Bengal";
        profile.ServiceAreaZone = "South Kolkata";
        profile.ServiceZoneId = SouthKolkataZoneId;
        profile.HourlyRate = 500;
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

    private sealed record AcceptedOrderSetup(
        string CustomerToken,
        string ProviderToken,
        Guid RequestId,
        Guid BidId,
        Guid OrderId,
        string CustomerId,
        string ProviderId,
        decimal AgreedAmount);

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}
