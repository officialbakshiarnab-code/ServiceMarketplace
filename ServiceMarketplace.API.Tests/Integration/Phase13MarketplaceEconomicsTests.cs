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

public class Phase13MarketplaceEconomicsTests : IClassFixture<ServiceMarketplaceWebApplicationFactory>
{
    private static readonly Guid PlumbingCategoryId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid SouthKolkataZoneId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    private readonly ServiceMarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public Phase13MarketplaceEconomicsTests(ServiceMarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PlatformPaymentIntent_AdminVerification_Completion_CreatesProviderPayout()
    {
        var setup = await CreateProviderCompletedOrderAsync(agreedAmount: 950m);

        SetAuthHeader(setup.CustomerToken);
        var directPlatformRecord = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/payment", new RecordServiceOrderPaymentDto
        {
            Method = PaymentMethod.Platform,
            Amount = setup.AgreedAmount
        });
        directPlatformRecord.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var intentResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/platform-payment-intent", null);
        intentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var intent = await ReadJsonAsync<PlatformPaymentIntentDto>(intentResponse);
        intent.Amount.Should().Be(950m);
        intent.PlatformFeeAmount.Should().Be(95m);
        intent.ProviderPayoutAmount.Should().Be(855m);
        intent.Status.Should().Be(PlatformPaymentIntentStatus.PendingVerification);

        var duplicateIntent = await _client.PostAsync($"/api/orders/{setup.OrderId}/platform-payment-intent", null);
        duplicateIntent.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        SetAuthHeader(setup.ProviderToken);
        var providerVerify = await _client.PostAsJsonAsync($"/api/admin/marketplace-economics/platform-payment-intents/{intent.Id}/verify", new VerifyPlatformPaymentIntentDto
        {
            GatewayPaymentId = "gw_provider_not_allowed"
        });
        providerVerify.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var verifyResponse = await _client.PostAsJsonAsync($"/api/admin/marketplace-economics/platform-payment-intents/{intent.Id}/verify", new VerifyPlatformPaymentIntentDto
        {
            GatewayPaymentId = "gw_phase13_paid_001",
            VerificationNotes = "Verified through gateway callback audit."
        });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await ReadJsonAsync<ServiceOrderPaymentDto>(verifyResponse);
        payment.Method.Should().Be(PaymentMethod.Platform);
        payment.Status.Should().Be(PaymentStatus.Held);
        payment.PlatformPaymentIntentId.Should().Be(intent.Id);
        payment.PlatformFeeAmount.Should().Be(95m);
        payment.ProviderPayoutAmount.Should().Be(855m);

        SetAuthHeader(setup.CustomerToken);
        var completeResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var dbContext = await _factory.GetDbContextAsync())
        {
            var payout = await dbContext.ProviderPayouts.SingleAsync(p => p.ServiceOrderId == setup.OrderId);
            payout.Status.Should().Be(ProviderPayoutStatus.Pending);
            payout.GrossAmount.Should().Be(950m);
            payout.PlatformFeeAmount.Should().Be(95m);
            payout.PayoutAmount.Should().Be(855m);
        }

        SetAuthHeader(adminToken);
        var payoutsResponse = await _client.GetAsync($"/api/admin/marketplace-economics/payouts?status={(int)ProviderPayoutStatus.Pending}");
        payoutsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payouts = await ReadJsonAsync<List<ProviderPayoutDto>>(payoutsResponse);
        var pendingPayout = payouts.Should().ContainSingle(p => p.ServiceOrderId == setup.OrderId).Subject;

        var markPaidResponse = await _client.PostAsJsonAsync($"/api/admin/marketplace-economics/payouts/{pendingPayout.Id}/mark-paid", new MarkProviderPayoutPaidDto
        {
            PayoutReference = "payout_phase13_001",
            Notes = "Paid through manual bank transfer."
        });
        markPaidResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paidPayout = await ReadJsonAsync<ProviderPayoutDto>(markPaidResponse);
        paidPayout.Status.Should().Be(ProviderPayoutStatus.Paid);
        paidPayout.PayoutReference.Should().Be("payout_phase13_001");
    }

    [Fact]
    public async Task HeldPlatformPayment_CanBeDisputed_AndAdminRefundBlocksCompletion()
    {
        var setup = await CreateProviderCompletedOrderAsync(agreedAmount: 1200m);
        var payment = await CreateVerifiedPlatformPaymentAsync(setup, "gw_phase13_dispute_001");

        SetAuthHeader(setup.CustomerToken);
        var disputeResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/disputes", new CreateServiceOrderDisputeDto
        {
            Reason = "Provider marked complete but work quality is disputed."
        });
        disputeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dispute = await ReadJsonAsync<ServiceOrderDisputeDto>(disputeResponse);
        dispute.Status.Should().Be(ServiceOrderDisputeStatus.Open);
        dispute.ServiceOrderPaymentId.Should().Be(payment.Id);

        var blockedCompletion = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        blockedCompletion.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var resolveResponse = await _client.PostAsJsonAsync($"/api/admin/marketplace-economics/disputes/{dispute.Id}/resolve", new ResolveServiceOrderDisputeDto
        {
            Status = ServiceOrderDisputeStatus.RefundedToCustomer,
            ResolutionNotes = "Refunded after review of submitted evidence."
        });
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await ReadJsonAsync<ServiceOrderDisputeDto>(resolveResponse);
        resolved.Status.Should().Be(ServiceOrderDisputeStatus.RefundedToCustomer);

        await using (var dbContext = await _factory.GetDbContextAsync())
        {
            var persistedPayment = await dbContext.ServiceOrderPayments.SingleAsync(p => p.Id == payment.Id);
            persistedPayment.Status.Should().Be(PaymentStatus.Refunded);
            (await dbContext.ProviderPayouts.AnyAsync(p => p.ServiceOrderPaymentId == payment.Id)).Should().BeFalse();

            var order = await dbContext.ServiceOrders.SingleAsync(o => o.Id == setup.OrderId);
            order.Status.Should().Be(ServiceOrderStatus.Cancelled);
            order.CancellationReason.Should().Be("Refunded after review of submitted evidence.");
        }

        SetAuthHeader(setup.CustomerToken);
        var refundedCompletion = await _client.PostAsync($"/api/orders/{setup.OrderId}/confirm-complete", null);
        refundedCompletion.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancellingOrderWithPendingPlatformIntent_CancelsIntentAndBlocksVerification()
    {
        var setup = await CreateProviderCompletedOrderAsync(agreedAmount: 700m);

        SetAuthHeader(setup.CustomerToken);
        var intentResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/platform-payment-intent", null);
        intentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var intent = await ReadJsonAsync<PlatformPaymentIntentDto>(intentResponse);

        var cancelResponse = await _client.PostAsJsonAsync($"/api/orders/{setup.OrderId}/cancel", new CancelServiceOrderDto
        {
            Reason = "Customer cancelled before platform payment was verified."
        });
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var dbContext = await _factory.GetDbContextAsync())
        {
            var persistedIntent = await dbContext.PlatformPaymentIntents.SingleAsync(i => i.Id == intent.Id);
            persistedIntent.Status.Should().Be(PlatformPaymentIntentStatus.Cancelled);
        }

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var verifyResponse = await _client.PostAsJsonAsync($"/api/admin/marketplace-economics/platform-payment-intents/{intent.Id}/verify", new VerifyPlatformPaymentIntentDto
        {
            GatewayPaymentId = "gw_phase13_cancelled_intent"
        });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<ServiceOrderPaymentDto> CreateVerifiedPlatformPaymentAsync(AcceptedOrderSetup setup, string gatewayPaymentId)
    {
        SetAuthHeader(setup.CustomerToken);
        var intentResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/platform-payment-intent", null);
        intentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var intent = await ReadJsonAsync<PlatformPaymentIntentDto>(intentResponse);

        var adminToken = await CreateAdminTokenAsync();
        SetAuthHeader(adminToken);
        var verifyResponse = await _client.PostAsJsonAsync($"/api/admin/marketplace-economics/platform-payment-intents/{intent.Id}/verify", new VerifyPlatformPaymentIntentDto
        {
            GatewayPaymentId = gatewayPaymentId,
            VerificationNotes = "Verified for dispute path."
        });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return await ReadJsonAsync<ServiceOrderPaymentDto>(verifyResponse);
    }

    private async Task<AcceptedOrderSetup> CreateProviderCompletedOrderAsync(decimal agreedAmount)
    {
        var setup = await CreateAcceptedOrderAsync(agreedAmount);

        SetAuthHeader(setup.ProviderToken);
        var startResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/start", null);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var providerCompleteResponse = await _client.PostAsync($"/api/orders/{setup.OrderId}/provider-complete", null);
        providerCompleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return setup;
    }

    private async Task<AcceptedOrderSetup> CreateAcceptedOrderAsync(decimal agreedAmount)
    {
        var customerEmail = $"phase13-customer{Guid.NewGuid():N}@test.com";
        var customerToken = await RegisterAndLoginAsync(customerEmail, RoleConstants.User);
        SetAuthHeader(customerToken);

        var requestResponse = await _client.PostAsJsonAsync("/api/requests", new CreateServiceRequestDto
        {
            Title = "Phase 13 marketplace economics request",
            Description = "Create a service order for platform economics coverage.",
            ServiceCategoryId = PlumbingCategoryId,
            ServiceZoneId = SouthKolkataZoneId,
            Location = "88 Phase Thirteen Address, Kolkata, West Bengal",
            Latitude = 22.501,
            Longitude = 88.361,
            Urgency = ServiceRequestUrgency.Soon,
            Requirements = "Phase 13 test."
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var requestPayload = await ReadJsonAsync<CreateServiceRequestResponse>(requestResponse);

        var providerEmail = $"phase13-provider{Guid.NewGuid():N}@test.com";
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
            Message = "Phase 13 provider bid."
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

        return new AcceptedOrderSetup(customerToken, providerToken, order.Id, agreedAmount);
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

    private async Task<string> CreateAdminTokenAsync()
    {
        var adminEmail = $"phase13-admin{Guid.NewGuid():N}@test.com";
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

        profile.DisplayName = "Phase 13 Provider";
        profile.BusinessName = "Phase 13 Services";
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
        Guid OrderId,
        decimal AgreedAmount);

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}
