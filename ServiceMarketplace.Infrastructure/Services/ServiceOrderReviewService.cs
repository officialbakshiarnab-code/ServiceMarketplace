using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServiceOrderReviewService(
    AppDbContext context,
    INotificationService notificationService,
    IServiceOrderAuditService auditService) : IServiceOrderReviewService
{
    public async Task<ServiceOrderReviewDto?> GetForOrderAsync(Guid orderId, string userId)
    {
        var order = await GetParticipantOrderAsync(orderId, userId, tracking: false);

        var review = await context.ServiceOrderReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ServiceOrderId == order.Id);

        return review == null ? null : ToDto(review);
    }

    public async Task<List<ServiceOrderReviewDto>> GetForProviderAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return [];

        var reviews = await context.ServiceOrderReviews
            .AsNoTracking()
            .Where(r => r.ProviderId == providerId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reviews.Select(ToDto).ToList();
    }

    public async Task<List<ServiceOrderReviewDto>> GetForAdminAsync(bool includeHidden = true)
    {
        var query = context.ServiceOrderReviews
            .AsNoTracking()
            .AsQueryable();

        if (!includeHidden)
            query = query.Where(r => !r.IsHidden);

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync();

        return reviews.Select(ToDto).ToList();
    }

    public async Task<ServiceOrderReviewDto> CreateAsync(Guid orderId, string customerId, CreateServiceOrderReviewDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var order = await GetParticipantOrderAsync(orderId, customerId, tracking: true);
            if (order.CustomerId != customerId)
                throw new ForbiddenException("Only the customer can review this order.");

            if (order.Status != ServiceOrderStatus.Completed)
                throw new BadRequestException("Reviews require a completed service order.");

            if (await context.ServiceOrderReviews.AnyAsync(r => r.ServiceOrderId == order.Id))
                throw new BadRequestException("This order already has a review.");

            if (dto.Rating is < 1 or > 5)
                throw new BadRequestException("Rating must be between 1 and 5.");

            var review = new ServiceOrderReview
            {
                ServiceOrderId = order.Id,
                CustomerId = order.CustomerId,
                ProviderId = order.ProviderId,
                Rating = dto.Rating,
                Feedback = NormalizeOptional(dto.Feedback, 2000)
            };

            context.ServiceOrderReviews.Add(review);
            await UpdateProviderAggregateAsync(order.ProviderId, dto.Rating);
            await context.SaveChangesAsync();
            await auditService.RecordAsync(
                order,
                customerId,
                "Customer",
                "ServiceOrderReviewCreated",
                null,
                null,
                $"{review.Rating}-star review created.");
            await notificationService.NotifyServiceOrderReviewReceivedAsync(review.Id);

            return ToDto(review);
        });
    }

    public async Task<ServiceOrderReviewDto> ModerateAsync(Guid reviewId, string adminUserId, ModerateServiceOrderReviewDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(adminUserId))
                throw new UnauthorizedAccessException("Admin identity is required.");

            var review = await context.ServiceOrderReviews
                .Include(r => r.ServiceOrder)
                .FirstOrDefaultAsync(r => r.Id == reviewId)
                ?? throw new NotFoundException("Review not found.");

            review.IsHidden = dto.IsHidden;
            review.ModerationNotes = NormalizeOptional(dto.ModerationNotes, 1000);
            review.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            await RecalculateProviderAggregateAsync(review.ProviderId);
            await context.SaveChangesAsync();
            await auditService.RecordAsync(
                review.ServiceOrder,
                adminUserId,
                "Admin",
                "ServiceOrderReviewModerated",
                null,
                review.IsHidden ? "Hidden" : "Visible",
                review.ModerationNotes);

            return ToDto(review);
        });
    }

    private async Task<ServiceOrder> GetParticipantOrderAsync(Guid orderId, string userId, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var query = context.ServiceOrders
            .Where(o => o.Id == orderId && (o.CustomerId == userId || o.ProviderId == userId));

        if (!tracking)
            query = query.AsNoTracking();

        var order = await query.FirstOrDefaultAsync();
        if (order == null)
            throw new NotFoundException("Service order not found.");

        return order;
    }

    private async Task UpdateProviderAggregateAsync(string providerId, int rating)
    {
        if (!Guid.TryParse(providerId, out var providerGuid))
            return;

        var profile = await context.ServiceProviderProfiles
            .FirstOrDefaultAsync(p => p.UserId == providerGuid);

        if (profile == null)
            return;

        var total = profile.AverageRating * profile.ReviewCount + rating;
        profile.ReviewCount += 1;
        profile.AverageRating = Math.Round(total / profile.ReviewCount, 2, MidpointRounding.AwayFromZero);
        profile.UpdatedAt = DateTime.UtcNow;
    }

    private async Task RecalculateProviderAggregateAsync(string providerId)
    {
        if (!Guid.TryParse(providerId, out var providerGuid))
            return;

        var profile = await context.ServiceProviderProfiles
            .FirstOrDefaultAsync(p => p.UserId == providerGuid);

        if (profile == null)
            return;

        var visibleReviews = await context.ServiceOrderReviews
            .Where(r => r.ProviderId == providerId && !r.IsHidden)
            .Select(r => r.Rating)
            .ToListAsync();

        profile.ReviewCount = visibleReviews.Count;
        profile.AverageRating = visibleReviews.Count == 0
            ? 0m
            : Math.Round(visibleReviews.Average(r => (decimal)r), 2, MidpointRounding.AwayFromZero);
        profile.UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length > maxLength)
            throw new BadRequestException($"Value cannot exceed {maxLength} characters.");

        return value;
    }

    private static ServiceOrderReviewDto ToDto(ServiceOrderReview review)
    {
        return new ServiceOrderReviewDto
        {
            Id = review.Id,
            ServiceOrderId = review.ServiceOrderId,
            CustomerId = review.CustomerId,
            ProviderId = review.ProviderId,
            Rating = review.Rating,
            Feedback = review.Feedback,
            IsHidden = review.IsHidden,
            ModerationNotes = review.ModerationNotes,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
    }
}
