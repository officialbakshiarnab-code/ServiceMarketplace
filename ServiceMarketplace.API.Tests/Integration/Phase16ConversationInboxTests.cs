using System.Net;
using System.Net.Http.Headers;
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

public class Phase16ConversationInboxTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase16ConversationInboxTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptedBid_CreatesOneParticipantOnlyConversation()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var customerInboxResponse = await _client.GetAsync("/api/conversations");
        customerInboxResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerInbox = await ReadJsonAsync<List<ConversationInboxItemDto>>(customerInboxResponse);
        var conversation = customerInbox.Should().ContainSingle(c => c.ServiceOrderId == setup.OrderId).Subject;
        conversation.ContextType.Should().Be(ConversationContextType.ServiceOrder);
        conversation.LastMessagePreview.Should().Contain("Order confirmed");

        SetAuthHeader(setup.ProviderToken);
        var providerDetailResponse = await _client.GetAsync($"/api/conversations/{conversation.Id}");
        providerDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var providerDetail = await ReadJsonAsync<ConversationDetailDto>(providerDetailResponse);
        providerDetail.ServiceOrderId.Should().Be(setup.OrderId);
        providerDetail.Participants.Should().HaveCount(2);

        var strangerToken = await RegisterAndLoginAsync($"phase16-stranger{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(strangerToken);
        var strangerResponse = await _client.GetAsync($"/api/conversations/{conversation.Id}");
        strangerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConversationMessages_ValidateTextAndSupportIdempotentClientMessageId()
    {
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);
        var clientMessageId = Guid.NewGuid().ToString("N");

        SetAuthHeader(setup.CustomerToken);
        var emptyResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "   "
        });
        emptyResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var oversizedResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = new string('x', 4001)
        });
        oversizedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var firstResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "Please confirm when you are on the way.",
            ClientMessageId = clientMessageId
        });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstMessage = await ReadJsonAsync<ConversationMessageDto>(firstResponse);
        firstMessage.Type.Should().Be(MessageType.Text);

        var retryResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "Please confirm when you are on the way.",
            ClientMessageId = clientMessageId
        });
        retryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var retryMessage = await ReadJsonAsync<ConversationMessageDto>(retryResponse);
        retryMessage.Id.Should().Be(firstMessage.Id);

        await using var dbContext = await _factory.GetDbContextAsync();
        var duplicateCount = await dbContext.Messages.CountAsync(m => m.ConversationId == conversationId && m.ClientMessageId == clientMessageId);
        duplicateCount.Should().Be(1);
    }

    [Fact]
    public async Task ReadStateAndPreferences_UpdateUnreadAndInboxVisibility()
    {
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "I will be available after 6 PM."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        SetAuthHeader(setup.ProviderToken);
        var unreadResponse = await _client.GetAsync("/api/conversations/unread-count");
        unreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unread = await ReadJsonAsync<ConversationUnreadCountDto>(unreadResponse);
        unread.Count.Should().BeGreaterThan(0);

        var readResponse = await _client.PostAsync($"/api/conversations/{conversationId}/read", null);
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterReadResponse = await _client.GetAsync("/api/conversations/unread-count");
        var afterRead = await ReadJsonAsync<ConversationUnreadCountDto>(afterReadResponse);
        afterRead.Count.Should().Be(0);

        var archiveResponse = await _client.PatchAsJsonAsync($"/api/conversations/{conversationId}/preferences", new UpdateConversationPreferencesDto
        {
            IsArchived = true,
            IsMuted = true
        });
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var defaultInbox = await ReadJsonAsync<List<ConversationInboxItemDto>>(await _client.GetAsync("/api/conversations"));
        defaultInbox.Should().NotContain(c => c.Id == conversationId);

        var archivedInbox = await ReadJsonAsync<List<ConversationInboxItemDto>>(await _client.GetAsync("/api/conversations?includeArchived=true"));
        archivedInbox.Should().Contain(c => c.Id == conversationId && c.IsArchived && c.IsMuted);
    }

    [Fact]
    public async Task LegacyServiceOrderMessages_AreMirroredIntoConversationInbox()
    {
        var setup = await CreateAcceptedOrderAsync();

        SetAuthHeader(setup.CustomerToken);
        var legacySendResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/messages", new CreateServiceOrderMessageDto
        {
            Body = "Legacy order thread message should appear in the new inbox."
        });
        legacySendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(setup.ProviderToken);
        var conversationId = await GetConversationIdAsync(setup.ProviderToken, setup.OrderId);

        var messagesResponse = await _client.GetAsync($"/api/conversations/{conversationId}/messages");
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await ReadJsonAsync<ConversationMessagesPageDto>(messagesResponse);
        messages.Items.Should().Contain(m =>
            m.Type == MessageType.Text &&
            m.Body == "Legacy order thread message should appear in the new inbox.");

        var unreadResponse = await _client.GetAsync("/api/conversations/unread-count");
        unreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unread = await ReadJsonAsync<ConversationUnreadCountDto>(unreadResponse);
        unread.Count.Should().BeGreaterThan(0);

        var legacyReadResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/messages/read", null);
        legacyReadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRead = await ReadJsonAsync<ConversationUnreadCountDto>(await _client.GetAsync("/api/conversations/unread-count"));
        afterRead.Count.Should().Be(0);
    }

    [Fact]
    public async Task MessageReports_AreParticipantOnlyAndAdminReviewable()
    {
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "This customer message can be reported by the provider."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await ReadJsonAsync<ConversationMessageDto>(sendResponse);

        var selfReportResponse = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages/{message.Id}/reports",
            new CreateMessageReportDto { Reason = "Testing self-report." });
        selfReportResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        SetAuthHeader(setup.ProviderToken);
        var reportResponse = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages/{message.Id}/reports",
            new CreateMessageReportDto
            {
                Reason = "Unsafe content",
                Details = "Provider flagged this message for admin review."
            });
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await ReadJsonAsync<MessageReportDto>(reportResponse);
        report.Status.Should().Be(MessageReportStatus.PendingReview);
        report.MessagePreview.Should().Contain("customer message");

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages/{message.Id}/reports",
            new CreateMessageReportDto { Reason = "Duplicate report." });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var strangerToken = await RegisterAndLoginAsync($"phase16-report-stranger{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(strangerToken);
        var outsiderResponse = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages/{message.Id}/reports",
            new CreateMessageReportDto { Reason = "Outsider report." });
        outsiderResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var queueResponse = await _client.GetAsync("/api/admin/message-reports");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await ReadJsonAsync<List<MessageReportDto>>(queueResponse);
        queue.Should().Contain(r => r.Id == report.Id);

        var reviewResponse = await _client.PostAsJsonAsync($"/api/admin/message-reports/{report.Id}/review", new ReviewMessageReportDto
        {
            Status = MessageReportStatus.ActionTaken,
            ReviewNotes = "Admin action recorded."
        });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewed = await ReadJsonAsync<MessageReportDto>(reviewResponse);
        reviewed.Status.Should().Be(MessageReportStatus.ActionTaken);
        reviewed.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MessageReport_AdminActionCanHideMessageAndRestrictConversation()
    {
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "This message should be hidden by admin action."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await ReadJsonAsync<ConversationMessageDto>(sendResponse);

        SetAuthHeader(setup.ProviderToken);
        var reportResponse = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages/{message.Id}/reports",
            new CreateMessageReportDto { Reason = "Unsafe content" });
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await ReadJsonAsync<MessageReportDto>(reportResponse);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var reviewResponse = await _client.PostAsJsonAsync($"/api/admin/message-reports/{report.Id}/review", new ReviewMessageReportDto
        {
            Status = MessageReportStatus.ActionTaken,
            ReviewNotes = "Hide unsafe content and pause the conversation.",
            HideMessage = true,
            RestrictConversation = true,
            LegalHoldUntil = DateTime.UtcNow.AddMonths(18)
        });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewed = await ReadJsonAsync<MessageReportDto>(reviewResponse);
        reviewed.MessageHidden.Should().BeTrue();
        reviewed.ConversationRestricted.Should().BeTrue();
        reviewed.LegalHoldUntil.Should().NotBeNull();

        SetAuthHeader(setup.ProviderToken);
        var messages = await ReadJsonAsync<ConversationMessagesPageDto>(await _client.GetAsync($"/api/conversations/{conversationId}/messages"));
        messages.Items.Should().Contain(m => m.Id == message.Id && m.IsHidden && m.Body == "Message hidden by admin.");

        var detail = await ReadJsonAsync<ConversationDetailDto>(await _client.GetAsync($"/api/conversations/{conversationId}"));
        detail.Status.Should().Be(ConversationStatus.Restricted);

        var blockedSend = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "Restricted conversations should not accept new messages."
        });
        blockedSend.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var dbContext = await _factory.GetDbContextAsync();
        var conversation = await dbContext.Conversations.FirstAsync(c => c.Id == conversationId);
        conversation.RetainUntil.Should().NotBeNull();
        conversation.RetainUntil.Should().BeOnOrAfter(reviewed.LegalHoldUntil!.Value);
    }

    [Fact]
    public async Task ConversationSend_RejectsBurstAbuse()
    {
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.CustomerToken);
        for (var i = 0; i < 20; i++)
        {
            var response = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
            {
                Body = $"Burst message {i}"
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var blocked = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "This message should be blocked by service-level chat safety."
        });

        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeviceRegistration_IsUserScopedAndDoesNotExposeToken()
    {
        var userToken = await RegisterAndLoginAsync($"phase16-device-user{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(userToken);

        var registerResponse = await _client.PostAsJsonAsync("/api/devices", new RegisterDeviceDto
        {
            Platform = DevicePlatform.Android,
            DeviceToken = "fake-fcm-device-token",
            DeviceName = "Kolkata Android Demo"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registration = await ReadJsonAsync<DeviceRegistrationDto>(registerResponse);
        registration.Platform.Should().Be(DevicePlatform.Android);
        registration.DeviceName.Should().Be("Kolkata Android Demo");

        var responseText = await registerResponse.Content.ReadAsStringAsync();
        responseText.Should().NotContain("fake-fcm-device-token");

        var updateResponse = await _client.PostAsJsonAsync("/api/devices", new RegisterDeviceDto
        {
            Platform = DevicePlatform.Android,
            DeviceToken = "fake-fcm-device-token",
            DeviceName = "Updated Android Demo"
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadJsonAsync<DeviceRegistrationDto>(updateResponse);
        updated.Id.Should().Be(registration.Id);
        updated.DeviceName.Should().Be("Updated Android Demo");

        var mineResponse = await _client.GetAsync("/api/devices");
        mineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = await ReadJsonAsync<List<DeviceRegistrationDto>>(mineResponse);
        mine.Should().ContainSingle(d => d.Id == registration.Id);

        var strangerToken = await RegisterAndLoginAsync($"phase16-device-stranger{Guid.NewGuid():N}@test.com", RoleConstants.User);
        SetAuthHeader(strangerToken);
        var strangerDelete = await _client.DeleteAsync($"/api/devices/{registration.Id}");
        strangerDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        SetAuthHeader(userToken);
        var deleteResponse = await _client.DeleteAsync($"/api/devices/{registration.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await ReadJsonAsync<List<DeviceRegistrationDto>>(await _client.GetAsync("/api/devices"));
        afterDelete.Should().Contain(d => d.Id == registration.Id && !d.IsActive);
    }

    [Fact]
    public async Task ConversationSend_PushesPrivacyMinimalPayloadToRecipientDevices()
    {
        RecordingPushNotificationSender.Reset();
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.ProviderToken);
        var deviceToken = $"fake-provider-device-{Guid.NewGuid():N}";
        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", new RegisterDeviceDto
        {
            Platform = DevicePlatform.Android,
            DeviceToken = deviceToken,
            DeviceName = "Provider push test device"
        });
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "Please check this order update."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await ReadJsonAsync<ConversationMessageDto>(sendResponse);

        var sent = RecordingPushNotificationSender.Sent.Should().ContainSingle(r => r.DeviceToken == deviceToken).Subject;
        sent.Payload.Type.Should().Be("conversation.message");
        sent.Payload.ConversationId.Should().Be(conversationId);
        sent.Payload.MessageId.Should().Be(message.Id);
    }

    [Fact]
    public async Task ConversationSend_DoesNotPushToMutedRecipient()
    {
        RecordingPushNotificationSender.Reset();
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.ProviderToken);
        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", new RegisterDeviceDto
        {
            Platform = DevicePlatform.Android,
            DeviceToken = $"muted-provider-device-{Guid.NewGuid():N}",
            DeviceName = "Muted provider push test device"
        });
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var muteResponse = await _client.PatchAsJsonAsync($"/api/conversations/{conversationId}/preferences", new UpdateConversationPreferencesDto
        {
            IsMuted = true,
            IsArchived = false
        });
        muteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "This order update should stay quiet for a muted recipient."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        RecordingPushNotificationSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationSend_PushFailureDoesNotFailPersistedMessage()
    {
        RecordingPushNotificationSender.Reset();
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.ProviderToken);
        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", new RegisterDeviceDto
        {
            Platform = DevicePlatform.Android,
            DeviceToken = $"failing-provider-device-{Guid.NewGuid():N}",
            DeviceName = "Provider failing push test device"
        });
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        RecordingPushNotificationSender.ThrowOnSend = true;
        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "This message should be saved even when push delivery fails."
        });

        RecordingPushNotificationSender.ThrowOnSend = false;
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await ReadJsonAsync<ConversationMessageDto>(sendResponse);

        await using var dbContext = await _factory.GetDbContextAsync();
        var persisted = await dbContext.Messages.AnyAsync(m => m.Id == message.Id && m.Body == message.Body);
        persisted.Should().BeTrue();
    }

    [Fact]
    public async Task ConversationSend_InvalidPushTokenDeactivatesDeviceWithoutFailingMessage()
    {
        RecordingPushNotificationSender.Reset();
        var setup = await CreateAcceptedOrderAsync();
        var conversationId = await GetConversationIdAsync(setup.CustomerToken, setup.OrderId);

        SetAuthHeader(setup.ProviderToken);
        var invalidToken = $"invalid-provider-device-{Guid.NewGuid():N}";
        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", new RegisterDeviceDto
        {
            Platform = DevicePlatform.Android,
            DeviceToken = invalidToken,
            DeviceName = "Invalid provider push test device"
        });
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var device = await ReadJsonAsync<DeviceRegistrationDto>(deviceResponse);

        RecordingPushNotificationSender.InvalidTokens.Add(invalidToken);

        SetAuthHeader(setup.CustomerToken);
        var sendResponse = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new SendConversationMessageDto
        {
            Body = "This message should persist while the bad token is deactivated."
        });
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbContext = await _factory.GetDbContextAsync();
        var registration = await dbContext.DeviceRegistrations.FirstAsync(d => d.Id == device.Id);
        registration.IsActive.Should().BeFalse();
    }

    private async Task<Guid> GetConversationIdAsync(string token, Guid orderId)
    {
        SetAuthHeader(token);
        var response = await _client.GetAsync("/api/conversations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversations = await ReadJsonAsync<List<ConversationInboxItemDto>>(response);
        return conversations.Should().ContainSingle(c => c.ServiceOrderId == orderId).Subject.Id;
    }

    private async Task<AcceptedOrderSetup> CreateAcceptedOrderAsync()
    {
        await SeedCatalogAsync();

        var customerEmail = $"phase16-customer{Guid.NewGuid():N}@test.com";
        var customerToken = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);
        SetAuthHeader(customerToken);

        var requestResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Phase 16 conversation request",
            Description = "Create a service order conversation from an accepted bid.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "16 Conversation Street, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Conversation test."
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requestPayload = await ReadJsonAsync<CreateServiceRequestResponse>(requestResponse);

        var providerEmail = $"phase16-provider{Guid.NewGuid():N}@test.com";
        await RegisterAsync(providerEmail, RoleConstants.ServiceProvider);
        await ApproveProviderAsync(providerEmail);
        var providerToken = await LoginAsync(providerEmail);
        SetAuthHeader(providerToken);

        var bidResponse = await _client.PostAsJsonAsync("/api/bids", new CreateBidDto
        {
            ServiceRequestId = requestPayload.RequestId,
            Amount = 950,
            ProposedDateTime = DateTime.UtcNow.AddHours(6),
            EstimatedDurationMinutes = 90,
            Message = "Available for the conversation test."
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

        return new AcceptedOrderSetup(customerToken, providerToken, order.Id);
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
        return JwtTestHelper.ExtractToken(response.Content) ?? throw new InvalidOperationException("No token returned.");
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        var adminEmail = $"phase16-admin{Guid.NewGuid():N}@test.com";
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
        user.UserType = UserType.Admin;
        user.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return await LoginAsync(adminEmail);
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

        profile.DisplayName = "Phase 16 Provider";
        profile.BusinessName = "Phase 16 Services";
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

    private async Task SeedCatalogAsync()
    {
        await using var dbContext = await _factory.GetDbContextAsync();

        if (!await dbContext.ServiceZones.AnyAsync(z => z.Id == SouthKolkataZoneId))
        {
            dbContext.ServiceZones.Add(new ServiceZone
            {
                Id = SouthKolkataZoneId,
                Country = "India",
                State = "West Bengal",
                City = "Kolkata",
                ZoneName = "South Kolkata",
                DisplayName = "South Kolkata, Kolkata, West Bengal",
                PinCodeRegion = "7000xx",
                IsActive = true,
                SortOrder = 20,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await dbContext.ServiceCategories.AnyAsync(c => c.Id == PlumbingCategoryId))
        {
            dbContext.ServiceCategories.Add(new ServiceCategory
            {
                Id = PlumbingCategoryId,
                Name = "Plumbing",
                Slug = "phase16-plumbing",
                Description = "Water and fitting repairs",
                IsActive = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private void SetAuthHeader(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Response body could not be deserialized.");
    }

    private sealed record AcceptedOrderSetup(string CustomerToken, string ProviderToken, Guid OrderId);

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}
