using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Services;
using Xunit;

namespace ServiceMarketplace.Postgres.Tests;

public sealed class RelationalReliabilityTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task StockOne_TwoConcurrentBuyers_ExactlyOneSucceeds()
    {
        // Repeat with independent rows; each contender owns a separate DI scope/DbContext/connection.
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var listing = await SeedListingAsync(1);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Buy()
            {
                using var scope = fixture.Services.CreateScope();
                await start.Task;
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IProductDeliveryOrderService>()
                        .CreateAsync(Guid.NewGuid().ToString(), OrderRequest(listing.Id));
                    return true;
                }
                catch (BadRequestException ex)
                {
                    Assert.True(ex.Message.Contains("stock", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("available"));
                    return false;
                }
            }
            var first = Buy();
            var second = Buy();
            start.SetResult();
            var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(1, results.Count(x => !x));
            using var verify = fixture.Services.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, (await db.ProductListings.SingleAsync(p => p.Id == listing.Id)).StockQuantity);
            Assert.Equal(1, await db.ProductDeliveryOrders.CountAsync(o => o.ProductListingId == listing.Id));
        }
    }

    [PostgresFact]
    public async Task ConcurrentCancellation_RestoresStockOnce()
    {
        var listing = await SeedListingAsync(1);
        var buyer = Guid.NewGuid().ToString();
        var order = await CreateOrderAsync(listing.Id, buyer);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Cancel()
        {
            using var scope = fixture.Services.CreateScope();
            await start.Task;
            try
            {
                await scope.ServiceProvider.GetRequiredService<IProductDeliveryOrderService>().CancelAsBuyerAsync(order.Id, buyer, "Changed plans");
                return true;
            }
            catch (BadRequestException) { return false; }
        }
        var first = Cancel();
        var second = Cancel();
        start.SetResult();
        Assert.Single((await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20))).Where(x => x));
        using var verify = fixture.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, (await db.ProductListings.SingleAsync(p => p.Id == listing.Id)).StockQuantity);
        Assert.Equal(ProductDeliveryStatus.Cancelled, (await db.ProductDeliveryOrders.SingleAsync(o => o.Id == order.Id)).Status);
        // Seller retry is already idempotent and must not add another unit.
        await verify.ServiceProvider.GetRequiredService<IProductDeliveryOrderService>().UpdateSellerStatusAsync(order.Id,
            listing.SellerId.ToString(), new UpdateProductDeliveryStatusDto { Status = ProductDeliveryStatus.Cancelled });
        await db.Entry(await db.ProductListings.SingleAsync(p => p.Id == listing.Id)).ReloadAsync();
        Assert.Equal(1, (await db.ProductListings.SingleAsync(p => p.Id == listing.Id)).StockQuantity);
    }

    [PostgresFact]
    public async Task CancellationOfDifferentOrders_DoesNotLoseStockRestoration()
    {
        var listing = await SeedListingAsync(2);
        var buyer = Guid.NewGuid().ToString();
        var first = await CreateOrderAsync(listing.Id, buyer);
        var second = await CreateOrderAsync(listing.Id, buyer);
        async Task Cancel(Guid id)
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IProductDeliveryOrderService>().CancelAsBuyerAsync(id, buyer, "Cancel");
        }
        await Task.WhenAll(Cancel(first.Id), Cancel(second.Id)).WaitAsync(TimeSpan.FromSeconds(20));
        using var verify = fixture.Services.CreateScope();
        Assert.Equal(2, (await verify.ServiceProvider.GetRequiredService<AppDbContext>().ProductListings.SingleAsync(p => p.Id == listing.Id)).StockQuantity);
    }

    [PostgresFact]
    public async Task RequiredInboxFailure_RollsBackOrderAndReservedStock()
    {
        var listing = await SeedListingAsync(1);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.NotifyProductDeliveryOrderCreatedAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("injected"));
        var service = new ProductDeliveryOrderService(db, notifications.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Guid.NewGuid().ToString(), OrderRequest(listing.Id)));
        using var verify = fixture.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, (await check.ProductListings.SingleAsync(p => p.Id == listing.Id)).StockQuantity);
        Assert.False(await check.ProductDeliveryOrders.AnyAsync(o => o.ProductListingId == listing.Id));
    }

    [PostgresFact]
    public async Task CancellationPersistenceFailure_RollsBackStatusAndRestoration()
    {
        var listing = await SeedListingAsync(1);
        var buyer = Guid.NewGuid().ToString();
        var order = await CreateOrderAsync(listing.Id, buyer);
        using var scope = fixture.Services.CreateScope();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.NotifyProductDeliveryOrderStatusChangedAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("injected"));
        var service = new ProductDeliveryOrderService(scope.ServiceProvider.GetRequiredService<AppDbContext>(), notifications.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsBuyerAsync(order.Id, buyer, "Cancel"));
        using var verify = fixture.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await check.ProductListings.SingleAsync(p => p.Id == listing.Id)).StockQuantity);
        Assert.Equal(ProductDeliveryStatus.PendingSellerConfirmation, (await check.ProductDeliveryOrders.SingleAsync(o => o.Id == order.Id)).Status);
    }

    [PostgresFact]
    public async Task BidAcceptance_ConversationFailureRollsBackCoreAndAudit()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = Request();
        var bid = new Bid { ServiceRequest = request, ServiceProviderId = Guid.NewGuid().ToString(), Amount = 100,
            ProposedDateTime = DateTime.UtcNow.AddDays(1), Status = BidStatus.Pending };
        db.Bids.Add(bid);
        await db.SaveChangesAsync();
        var conversations = new Mock<IConversationService>();
        conversations.Setup(c => c.EnsureServiceOrderConversationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("injected conversation failure"));
        var service = new ServiceRequestService(db, scope.ServiceProvider.GetRequiredService<INotificationService>(),
            scope.ServiceProvider.GetRequiredService<IServiceOrderAuditService>(), conversations.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptBidAsync(request.Id, bid.Id, request.CustomerId));
        using var verify = fixture.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ServiceRequestStatus.Open, (await check.ServiceRequests.SingleAsync(r => r.Id == request.Id)).Status);
        Assert.Equal(BidStatus.Pending, (await check.Bids.SingleAsync(b => b.Id == bid.Id)).Status);
        Assert.False(await check.ServiceOrders.AnyAsync(o => o.ServiceRequestId == request.Id));
        Assert.False(await check.ServiceOrderAuditEvents.AnyAsync(e => e.ServiceRequestId == request.Id));
    }

    [PostgresFact]
    public async Task PaymentRecording_AuditFailureRollsBackPayment()
    {
        var order = await SeedOrderAsync(ServiceOrderStatus.ProviderCompleted);
        using var scope = fixture.Services.CreateScope();
        var service = new ServiceOrderPaymentService(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<INotificationService>(), FailingAudit(), scope.ServiceProvider.GetRequiredService<IConversationService>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(order.Id, order.CustomerId,
            new RecordServiceOrderPaymentDto { Amount = order.AgreedAmount, Method = PaymentMethod.Cash }));
        using var verify = fixture.Services.CreateScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<AppDbContext>().ServiceOrderPayments.AnyAsync(p => p.ServiceOrderId == order.Id));
    }

    [PostgresFact]
    public async Task Completion_AuditFailureRollsBackReleaseAndPayout()
    {
        var order = await SeedOrderAsync(ServiceOrderStatus.ProviderCompleted);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ServiceOrderPayments.Add(new ServiceOrderPayment
        {
            ServiceOrderId = order.Id, CustomerId = order.CustomerId, ProviderId = order.ProviderId,
            Amount = order.AgreedAmount, Method = PaymentMethod.Platform, Status = PaymentStatus.Held, RecordedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new ServiceOrderService(db, scope.ServiceProvider.GetRequiredService<INotificationService>(),
            FailingAudit(), scope.ServiceProvider.GetRequiredService<IConversationService>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmCustomerCompletionAsync(order.Id, order.CustomerId));
        using var verify = fixture.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ServiceOrderStatus.ProviderCompleted, (await check.ServiceOrders.SingleAsync(o => o.Id == order.Id)).Status);
        Assert.Equal(PaymentStatus.Held, (await check.ServiceOrderPayments.SingleAsync(p => p.ServiceOrderId == order.Id)).Status);
        Assert.False(await check.ProviderPayouts.AnyAsync(p => p.ServiceOrderId == order.Id));
    }

    [PostgresFact]
    public async Task SignalRFailure_IsAfterCommit_AndDoesNotFailOrderStartOrMessage()
    {
        var order = await SeedOrderAsync(ServiceOrderStatus.PendingStart);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var observed = 0;
        var notifier = new Mock<IConversationRealtimeNotifier>();
        notifier.Setup(n => n.MessageCreatedAsync(It.IsAny<ConversationRealtimeUpdateDto>(), It.IsAny<CancellationToken>()))
            .Returns(async (ConversationRealtimeUpdateDto update, CancellationToken _) =>
            {
                Assert.Null(db.Database.CurrentTransaction);
                using var reader = fixture.Services.CreateScope();
                Assert.True(await reader.ServiceProvider.GetRequiredService<AppDbContext>().Messages.AnyAsync(m => m.Id == update.MessageId));
                observed++;
                throw new HttpRequestException("injected publication outage");
            });
        var conversations = new ConversationService(db, notifier.Object, scope.ServiceProvider.GetRequiredService<IPushNotificationSender>(),
            NullLogger<ConversationService>.Instance);
        var service = new ServiceOrderService(db, scope.ServiceProvider.GetRequiredService<INotificationService>(),
            scope.ServiceProvider.GetRequiredService<IServiceOrderAuditService>(), conversations);
        var started = await service.StartAsync(order.Id, order.ProviderId);
        Assert.Equal(ServiceOrderStatus.InProgress, started.Status);
        var conversationId = await db.Conversations.Where(c => c.ServiceOrderId == order.Id).Select(c => c.Id).SingleAsync();
        var message = await conversations.SendMessageAsync(conversationId, order.CustomerId, new SendConversationMessageDto { Body = "Hello", ClientMessageId = "unique-1" });
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(2, observed);
        Assert.True(await db.ServiceOrderAuditEvents.AnyAsync(e => e.ServiceOrderId == order.Id));
    }

    [PostgresFact]
    public async Task UserNormalizedEmailAndPhone_AreUnique()
    {
        var user = NewUser();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        foreach (var emailDuplicate in new[] { true, false })
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var duplicate = NewUser();
            if (emailDuplicate) duplicate.NormalizedEmail = user.NormalizedEmail;
            else duplicate.NormalizedPhoneNumber = user.NormalizedPhoneNumber;
            db.Users.Add(duplicate);
            await AssertSqlStateAsync(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
    }

    [PostgresFact]
    public async Task PaymentAndReview_AreUniquePerOrder()
    {
        var order = await SeedOrderAsync(ServiceOrderStatus.Completed);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ServiceOrderPayments.Add(new ServiceOrderPayment { ServiceOrderId = order.Id, RecordedAt = DateTime.UtcNow });
            db.ServiceOrderReviews.Add(new ServiceOrderReview { ServiceOrderId = order.Id, Rating = 5 });
            await db.SaveChangesAsync();
        }
        foreach (var payment in new[] { true, false })
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (payment) db.ServiceOrderPayments.Add(new ServiceOrderPayment { ServiceOrderId = order.Id, RecordedAt = DateTime.UtcNow });
            else db.ServiceOrderReviews.Add(new ServiceOrderReview { ServiceOrderId = order.Id, Rating = 3 });
            await AssertSqlStateAsync(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
    }

    [PostgresFact]
    public async Task ProviderAndSellerProfiles_AreUniquePerUser()
    {
        var user = NewUser();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            db.ServiceProviderProfiles.Add(new ServiceProviderProfile { UserId = user.Id });
            db.SellerProfiles.Add(new SellerProfile { UserId = user.Id });
            await db.SaveChangesAsync();
        }
        foreach (var provider in new[] { true, false })
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (provider) db.ServiceProviderProfiles.Add(new ServiceProviderProfile { UserId = user.Id });
            else db.SellerProfiles.Add(new SellerProfile { UserId = user.Id });
            await AssertSqlStateAsync(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
    }

    [PostgresFact]
    public async Task ForeignKeys_RestrictOrderedListingDeletion_AndRejectMissingCategory()
    {
        var listing = await SeedListingAsync(1);
        await CreateOrderAsync(listing.Id, Guid.NewGuid().ToString());
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ProductListings.Remove(await db.ProductListings.SingleAsync(p => p.Id == listing.Id));
            await AssertSqlStateAsync(db.SaveChangesAsync, PostgresErrorCodes.RestrictViolation);
        }
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ProductListings.Add(new ProductListing { SellerId = listing.SellerId, ProductCategoryId = Guid.NewGuid() });
            await AssertSqlStateAsync(db.SaveChangesAsync, PostgresErrorCodes.ForeignKeyViolation);
        }
    }

    [PostgresFact]
    public async Task DecimalPrecisionAndNpgsqlProductQuery_AreVerified()
    {
        var listing = await SeedListingAsync(1, 12.345m);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(12.35m, (await db.ProductListings.SingleAsync(p => p.Id == listing.Id)).Price);
        var items = await scope.ServiceProvider.GetRequiredService<IProductListingService>().GetActiveAsync();
        Assert.Contains(items, p => p.Id == listing.Id && p.Price == 12.35m);
    }

    [PostgresFact]
    public async Task ConcurrentRefresh_ConsumesTokenOnce_AndReuseRevokesFamily()
    {
        var user = NewUser();
        string token;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            await db.SaveChangesAsync();
            token = await scope.ServiceProvider.GetRequiredService<ITokenRefreshService>().IssueRefreshTokenAsync(user.Id.ToString(), "session", null, null);
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Refresh()
        {
            using var scope = fixture.Services.CreateScope();
            await start.Task;
            try { await scope.ServiceProvider.GetRequiredService<ITokenRefreshService>().RefreshAccessTokenAsync(token, null, null); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var first = Refresh();
        var second = Refresh();
        start.SetResult();
        Assert.Single((await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20))).Where(x => x));
        using var verify = fixture.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await check.RefreshTokens.CountAsync(t => t.UserId == user.Id.ToString()));
        Assert.False(await check.RefreshTokens.AnyAsync(t => t.UserId == user.Id.ToString() && t.RevokedAt == null));
    }

    [PostgresFact]
    public async Task RefreshTokenHash_IsUnique_AndRotationRejectsReuse()
    {
        var user = NewUser();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<ITokenRefreshService>();
        var original = await service.IssueRefreshTokenAsync(user.Id.ToString(), Guid.NewGuid().ToString(), null, null);
        var rotated = await service.RefreshAccessTokenAsync(original, null, null);
        Assert.NotEqual(original, rotated.RefreshToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAccessTokenAsync(original, null, null));
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == user.Id.ToString() && t.RevokedAt == null));
        var persisted = await db.RefreshTokens.AsNoTracking().FirstAsync(t => t.UserId == user.Id.ToString());
        Assert.NotEqual(original, persisted.TokenHash);
        using var duplicateScope = fixture.Services.CreateScope();
        var duplicateDb = duplicateScope.ServiceProvider.GetRequiredService<AppDbContext>();
        duplicateDb.RefreshTokens.Add(new RefreshTokenEntity { TokenHash = persisted.TokenHash, ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await AssertSqlStateAsync(duplicateDb.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
    }

    private static IServiceOrderAuditService FailingAudit()
    {
        var audit = new Mock<IServiceOrderAuditService>();
        audit.Setup(a => a.RecordAsync(It.IsAny<ServiceOrder>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).ThrowsAsync(new InvalidOperationException("injected audit failure"));
        return audit.Object;
    }
    private static async Task AssertSqlStateAsync(Func<CancellationToken, Task<int>> save, string expected)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => save(CancellationToken.None));
        Assert.Equal(expected, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }
    private static User NewUser()
    {
        var id = Guid.NewGuid();
        return new User { Id = id, FirstName = "Test", LastName = "User", PasswordHash = "test-fixture-only",
            Email = id + "@example.invalid", NormalizedEmail = (id + "@example.invalid").ToUpperInvariant(),
            NormalizedPhoneNumber = id.ToString("N")[..15], DateOfBirth = DateTime.UtcNow.AddYears(-30) };
    }
    private async Task<ProductListing> SeedListingAsync(int stock, decimal price = 100)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = NewUser();
        var seller = new SellerProfile { User = user, UserId = user.Id, StoreName = "Test store", Status = SellerApplicationStatus.Approved };
        var listing = new ProductListing { SellerProfile = seller, SellerId = user.Id,
            ProductCategoryId = await db.ProductCategories.Select(c => c.Id).FirstAsync(),
            Title = "Test product", Description = "Fixture", StockQuantity = stock, Price = price, Status = ProductListingStatus.Active };
        db.ProductListings.Add(listing);
        await db.SaveChangesAsync();
        return listing;
    }
    private async Task<ProductDeliveryOrderDto> CreateOrderAsync(Guid listingId, string buyer)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProductDeliveryOrderService>().CreateAsync(buyer, OrderRequest(listingId));
    }
    private static CreateProductDeliveryOrderDto OrderRequest(Guid listingId) => new()
    {
        ProductListingId = listingId, Quantity = 1, DeliveryRecipientName = "Buyer", DeliveryPhoneNumber = "9876543210",
        DeliveryAddress = "Test address", DeliveryCity = "Kolkata", DeliveryState = "West Bengal"
    };
    private static ServiceRequest Request() => new() { CustomerId = Guid.NewGuid().ToString(), Title = "Repair", Description = "Fixture",
        Category = "Plumbing", Location = "Kolkata", Status = ServiceRequestStatus.Open };
    private async Task<ServiceOrder> SeedOrderAsync(ServiceOrderStatus status)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = Request();
        request.Status = ServiceRequestStatus.Accepted;
        var order = new ServiceOrder { ServiceRequest = request, CustomerId = request.CustomerId,
            ProviderId = Guid.NewGuid().ToString(), AgreedAmount = 100, ScheduledStartAt = DateTime.UtcNow.AddDays(1), Status = status };
        db.ServiceOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
