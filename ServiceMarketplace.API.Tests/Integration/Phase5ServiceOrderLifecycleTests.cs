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

public class Phase5ServiceOrderLifecycleTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase5ServiceOrderLifecycleTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptBid_CreatesServiceOrderVisibleToCustomerAndProvider()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var customerResponse = await _client.GetAsync("/api/orders/customer");
        customerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerOrders = await ReadJsonAsync<List<ServiceOrderDto>>(customerResponse);

        var customerOrder = customerOrders.Should().ContainSingle(o => o.ServiceRequestId == setup.RequestId).Subject;
        customerOrder.Status.Should().Be(ServiceOrderStatus.PendingStart);
        customerOrder.AcceptedBidId.Should().Be(setup.BidId);
        customerOrder.ExactLocation.Should().Be("55 Phase Five Address, Kolkata, West Bengal");
        customerOrder.AgreedAmount.Should().Be(850);

        SetAuthHeader(setup.ProviderToken);
        var providerResponse = await _client.GetAsync("/api/orders/provider");
        providerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerOrders = await ReadJsonAsync<List<ServiceOrderDto>>(providerResponse);

        providerOrders.Should().ContainSingle(o => o.Id == customerOrder.Id);
    }

    [Fact]
    public async Task ServiceOrder_CanMoveFromStartToProviderCompleteToCustomerConfirmed()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.ProviderToken);
        var startResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await ReadJsonAsync<ServiceOrderDto>(startResponse);
        started.Status.Should().Be(ServiceOrderStatus.InProgress);

        var providerCompleteResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/provider-complete", null);
        providerCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerCompleted = await ReadJsonAsync<ServiceOrderDto>(providerCompleteResponse);
        providerCompleted.Status.Should().Be(ServiceOrderStatus.ProviderCompleted);

        SetAuthHeader(setup.CustomerToken);
        var paymentResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.DirectUpi,
            Amount = 850,
            ReferenceNumber = "PHASE5-UPI"
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await ReadJsonAsync<ServiceOrderDto>(confirmResponse);
        completed.Status.Should().Be(ServiceOrderStatus.Completed);
        completed.CompletedAt.Should().NotBeNull();

        await using var dbContext = await _factory.GetDbContextAsync();
        var request = await dbContext.ServiceRequests.FirstAsync(r => r.Id == setup.RequestId);
        request.Status.Should().Be(ServiceRequestStatus.Closed);
    }

    [Fact]
    public async Task NonParticipant_CannotViewServiceOrder()
    {
        var setup = await CreateAcceptedOrderAsync();
        var strangerToken = await RegisterAndLoginAsync($"phase5-stranger{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(strangerToken);

        var response = await _client.GetAsync($"/api/orders/{setup.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ParticipantCanCancelActiveOrder_AndRequestIsClosed()
    {
        var setup = await CreateAcceptedOrderAsync();
        SetAuthHeader(setup.ProviderToken);

        var response = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/cancel", new CancelServiceOrderDto
        {
            Reason = "Provider is unavailable."
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await ReadJsonAsync<ServiceOrderDto>(response);
        cancelled.Status.Should().Be(ServiceOrderStatus.Cancelled);
        cancelled.CancellationReason.Should().Be("Provider is unavailable.");
        cancelled.CancelledByUserId.Should().NotBeNullOrWhiteSpace();

        await using var dbContext = await _factory.GetDbContextAsync();
        var request = await dbContext.ServiceRequests.FirstAsync(r => r.Id == setup.RequestId);
        request.Status.Should().Be(ServiceRequestStatus.Closed);
    }

    private async Task<AcceptedOrderSetup> CreateAcceptedOrderAsync()
    {
        var customerEmail = $"phase5-customer{Guid.NewGuid():N}@test.com";
        var customerToken = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);
        SetAuthHeader(customerToken);

        var requestResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Phase 5 lifecycle request",
            Description = "Create a service order from the accepted bid.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "55 Phase Five Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Lifecycle test."
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requestPayload = await ReadJsonAsync<CreateServiceRequestResponse>(requestResponse);

        var providerEmail = $"phase5-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        var providerToken = await LoginAsync(providerEmail);
        SetAuthHeader(providerToken);

        var bidResponse = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestPayload.RequestId,
            Amount = 850,
            ProposedDateTime = DateTime.UtcNow.AddHours(6),
            EstimatedDurationMinutes = 120,
            Message = "Ready to start."
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

        return new AcceptedOrderSetup(customerToken, providerToken, requestPayload.RequestId, bidId, order.Id);
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

        profile.DisplayName = "Phase 5 Provider";
        profile.BusinessName = "Phase 5 Services";
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
        Guid OrderId);

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}
