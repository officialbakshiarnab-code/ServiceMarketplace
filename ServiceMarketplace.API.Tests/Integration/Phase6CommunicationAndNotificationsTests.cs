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

public class Phase6CommunicationAndNotificationsTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase6CommunicationAndNotificationsTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptBid_CreatesCustomerAndProviderNotifications()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.ProviderToken);
        var providerResponse = await _client.GetAsync("/api/notifications");
        providerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerNotifications = await ReadJsonAsync<List<UserNotificationDto>>(providerResponse);

        providerNotifications.Should().Contain(n =>
            n.Type == UserNotificationType.BidAccepted &&
            n.BidId == setup.BidId &&
            n.ServiceRequestId == setup.RequestId);
        providerNotifications.Should().Contain(n =>
            n.Type == UserNotificationType.ServiceOrderCreated &&
            n.ServiceOrderId == setup.OrderId);

        SetAuthHeader(setup.CustomerToken);
        var customerResponse = await _client.GetAsync("/api/notifications");
        customerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerNotifications = await ReadJsonAsync<List<UserNotificationDto>>(customerResponse);

        customerNotifications.Should().ContainSingle(n =>
            n.Type == UserNotificationType.ServiceOrderCreated &&
            n.ServiceOrderId == setup.OrderId);
    }

    [Fact]
    public async Task ServiceOrderLifecycle_EmitsNotificationsForOppositeParticipant()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.ProviderToken);
        var startResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerCompleteResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/provider-complete", null);
        providerCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(setup.CustomerToken);
        var customerNotificationsResponse = await _client.GetAsync("/api/notifications");
        customerNotificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerNotifications = await ReadJsonAsync<List<UserNotificationDto>>(customerNotificationsResponse);

        customerNotifications.Should().Contain(n =>
            n.Type == UserNotificationType.ServiceOrderStarted &&
            n.ServiceOrderId == setup.OrderId);
        customerNotifications.Should().Contain(n =>
            n.Type == UserNotificationType.ServiceOrderProviderCompleted &&
            n.ServiceOrderId == setup.OrderId);

        var paymentResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.Cash,
            Amount = 900,
            Notes = "Paid after provider completion."
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(setup.ProviderToken);
        var providerNotificationsResponse = await _client.GetAsync("/api/notifications");
        providerNotificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerNotifications = await ReadJsonAsync<List<UserNotificationDto>>(providerNotificationsResponse);

        providerNotifications.Should().Contain(n =>
            n.Type == UserNotificationType.ServiceOrderCompleted &&
            n.ServiceOrderId == setup.OrderId);
    }

    [Fact]
    public async Task ParticipantsCanMessageEachOther_AndRecipientCanMarkThreadRead()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/messages", new CreateServiceOrderMessageDto
        {
            Body = "I will be available after 3 PM."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sent = await ReadJsonAsync<ServiceOrderMessageDto>(sendResponse);

        sent.SenderUserId.Should().Be(setup.CustomerId);
        sent.RecipientUserId.Should().Be(setup.ProviderId);
        sent.Body.Should().Be("I will be available after 3 PM.");
        sent.ReadAt.Should().BeNull();

        SetAuthHeader(setup.ProviderToken);
        var messagesResponse = await _client.GetAsync($"/api/orders/{setup.OrderId}/messages");
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await ReadJsonAsync<List<ServiceOrderMessageDto>>(messagesResponse);
        messages.Should().ContainSingle(m => m.Id == sent.Id);

        var notificationResponse = await _client.GetAsync("/api/notifications?unreadOnly=true");
        notificationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await ReadJsonAsync<List<UserNotificationDto>>(notificationResponse);
        var messageNotification = notifications.Should().ContainSingle(n =>
            n.Type == UserNotificationType.OrderMessageReceived &&
            n.ServiceOrderMessageId == sent.Id).Subject;
        messageNotification.IsRead.Should().BeFalse();

        var readResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/messages/read", null);
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var dbContext = await _factory.GetDbContextAsync();
        var persistedMessage = await dbContext.ServiceOrderMessages.SingleAsync(m => m.Id == sent.Id);
        persistedMessage.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NonParticipant_CannotUseOrderMessageThread()
    {
        var setup = await CreateAcceptedOrderAsync();
        var strangerToken = await RegisterAndLoginAsync($"phase6-stranger{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(strangerToken);

        var getResponse = await _client.GetAsync($"/api/orders/{setup.OrderId}/messages");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var sendResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/messages", new CreateServiceOrderMessageDto
        {
            Body = "Can I join this order?"
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<AcceptedOrderSetup> CreateAcceptedOrderAsync()
    {
        var customerEmail = $"phase6-customer{Guid.NewGuid():N}@test.com";
        var customerToken = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);
        SetAuthHeader(customerToken);

        var requestResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Phase 6 communication request",
            Description = "Create a service order with communication and notifications.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "66 Phase Six Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Communication test."
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requestPayload = await ReadJsonAsync<CreateServiceRequestResponse>(requestResponse);

        var providerEmail = $"phase6-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        var providerToken = await LoginAsync(providerEmail);
        SetAuthHeader(providerToken);

        var bidResponse = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestPayload.RequestId,
            Amount = 900,
            ProposedDateTime = DateTime.UtcNow.AddHours(5),
            EstimatedDurationMinutes = 90,
            Message = "Phase 6 provider bid."
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
            order.ProviderId);
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

        profile.DisplayName = "Phase 6 Provider";
        profile.BusinessName = "Phase 6 Services";
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
        string ProviderId);

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}
