using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ContactRequestService(
    AppDbContext context,
    INotificationService notificationService) : IContactRequestService
{
    public async Task<ContactRequestDto> CreateAsync(string requesterUserId, CreateContactRequestDto request)
    {
        if (!Guid.TryParse(requesterUserId, out var requesterId) ||
            !Guid.TryParse(request.TargetUserId, out var targetId))
            throw new BadRequestException("Invalid user id.");

        if (requesterId == targetId)
            throw new BadRequestException("You cannot request your own contact details.");

        var requesterExists = await context.Users.AnyAsync(u => u.Id == requesterId && u.IsActive);
        if (!requesterExists)
            throw new NotFoundException("Requester not found.");

        await EnsureTargetProfileAsync(targetId, request.TargetProfileType);
        await EnsureNoPendingDuplicateAsync(requesterUserId, request);

        var contactRequest = new ContactRequest
        {
            RequesterUserId = requesterUserId,
            TargetUserId = request.TargetUserId,
            TargetProfileType = request.TargetProfileType,
            Kind = request.Kind,
            Status = ContactRequestStatus.PendingAdminReview,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            PreferredCallbackAt = request.Kind == ContactRequestKind.Callback ? request.PreferredCallbackAt : null,
            CreatedAt = DateTime.UtcNow
        };

        context.ContactRequests.Add(contactRequest);
        await context.SaveChangesAsync();

        if (contactRequest.Kind == ContactRequestKind.Callback)
            await notificationService.NotifyCallbackRequestedAsync(contactRequest.Id);
        else
            await notificationService.NotifyContactRequestSubmittedAsync(contactRequest.Id);

        return await GetAsync(contactRequest.Id, requesterUserId);
    }

    public async Task<List<ContactRequestDto>> GetSentAsync(string userId)
    {
        var requests = await context.ContactRequests
            .AsNoTracking()
            .Where(r => r.RequesterUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync();

        return await ToDtosAsync(requests, userId);
    }

    public async Task<List<ContactRequestDto>> GetReceivedAsync(string userId)
    {
        var requests = await context.ContactRequests
            .AsNoTracking()
            .Where(r => r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync();

        return await ToDtosAsync(requests, userId);
    }

    public async Task<List<ContactRequestDto>> GetAdminQueueAsync(ContactRequestStatus? status)
    {
        var query = context.ContactRequests.AsNoTracking();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync();

        return await ToDtosAsync(requests, viewerUserId: string.Empty, isAdmin: true);
    }

    public async Task<ContactRequestDto> GetAsync(Guid id, string userId, bool isAdmin = false)
    {
        var request = await context.ContactRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Contact request not found.");

        if (!isAdmin && !CanAccess(request, userId))
            throw new ForbiddenException("You cannot view this contact request.");

        return (await ToDtosAsync([request], userId, isAdmin)).Single();
    }

    public async Task<ContactRequestDto> ReviewAsync(Guid id, string adminUserId, ReviewContactRequestDto request)
    {
        if (request.Status is not (ContactRequestStatus.Approved or ContactRequestStatus.Rejected))
            throw new BadRequestException("Admin review must approve or reject the request.");

        var contactRequest = await context.ContactRequests.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Contact request not found.");

        if (contactRequest.Status != ContactRequestStatus.PendingAdminReview)
            throw new BadRequestException("Only pending contact requests can be reviewed.");

        contactRequest.Status = request.Status;
        contactRequest.ReviewedByUserId = adminUserId;
        contactRequest.ReviewedAt = DateTime.UtcNow;
        contactRequest.ReviewNotes = string.IsNullOrWhiteSpace(request.ReviewNotes) ? null : request.ReviewNotes.Trim();
        contactRequest.RejectionReason = request.Status == ContactRequestStatus.Rejected
            ? string.IsNullOrWhiteSpace(request.RejectionReason) ? "Rejected by admin review." : request.RejectionReason.Trim()
            : null;
        contactRequest.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        if (contactRequest.Status == ContactRequestStatus.Approved)
            await notificationService.NotifyContactRequestApprovedAsync(contactRequest.Id);
        else
            await notificationService.NotifyContactRequestRejectedAsync(contactRequest.Id);

        return await GetAsync(contactRequest.Id, adminUserId, isAdmin: true);
    }

    public async Task<ContactRequestDto> CancelAsync(Guid id, string userId)
    {
        var contactRequest = await context.ContactRequests.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Contact request not found.");

        if (contactRequest.RequesterUserId != userId)
            throw new ForbiddenException("Only the requester can cancel this contact request.");

        if (contactRequest.Status != ContactRequestStatus.PendingAdminReview)
            throw new BadRequestException("Only pending contact requests can be cancelled.");

        contactRequest.Status = ContactRequestStatus.Cancelled;
        contactRequest.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return await GetAsync(contactRequest.Id, userId);
    }

    public async Task<ContactRequestDto> CompleteAsync(Guid id, string userId)
    {
        var contactRequest = await context.ContactRequests.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Contact request not found.");

        if (!CanAccess(contactRequest, userId))
            throw new ForbiddenException("You cannot complete this contact request.");

        if (contactRequest.Status != ContactRequestStatus.Approved)
            throw new BadRequestException("Only approved contact requests can be completed.");

        contactRequest.Status = ContactRequestStatus.Completed;
        contactRequest.CompletedByUserId = userId;
        contactRequest.CompletedAt = DateTime.UtcNow;
        contactRequest.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return await GetAsync(contactRequest.Id, userId);
    }

    public async Task<ContactRequestSummaryDto> GetSummaryAsync(string userId)
    {
        var relevant = context.ContactRequests
            .AsNoTracking()
            .Where(r => r.RequesterUserId == userId || r.TargetUserId == userId);

        return new ContactRequestSummaryDto
        {
            PendingCount = await relevant.CountAsync(r => r.Status == ContactRequestStatus.PendingAdminReview),
            ApprovedCount = await relevant.CountAsync(r => r.Status == ContactRequestStatus.Approved),
            RejectedCount = await relevant.CountAsync(r => r.Status == ContactRequestStatus.Rejected),
            CallbackPendingCount = await relevant.CountAsync(r =>
                r.Kind == ContactRequestKind.Callback &&
                r.Status == ContactRequestStatus.PendingAdminReview)
        };
    }

    private async Task EnsureTargetProfileAsync(Guid targetId, ContactProfileType profileType)
    {
        var targetExists = await context.Users.AnyAsync(u => u.Id == targetId && u.IsActive);
        if (!targetExists)
            throw new NotFoundException("Target user not found.");

        var hasApprovedProviderProfile = await context.ServiceProviderProfiles.AnyAsync(p =>
            p.UserId == targetId && p.Status == ProviderApplicationStatus.Approved);
        var hasApprovedSellerProfile = await context.SellerProfiles.AnyAsync(s =>
            s.UserId == targetId && s.Status == SellerApplicationStatus.Approved);

        var validProfile = profileType switch
        {
            ContactProfileType.User => !hasApprovedProviderProfile && !hasApprovedSellerProfile,
            ContactProfileType.Provider => hasApprovedProviderProfile,
            ContactProfileType.Seller => hasApprovedSellerProfile,
            _ => false
        };

        if (!validProfile)
            throw new NotFoundException("Target profile not found.");
    }

    private async Task EnsureNoPendingDuplicateAsync(string requesterUserId, CreateContactRequestDto request)
    {
        var duplicateExists = await context.ContactRequests.AnyAsync(r =>
            r.RequesterUserId == requesterUserId &&
            r.TargetUserId == request.TargetUserId &&
            r.TargetProfileType == request.TargetProfileType &&
            r.Kind == request.Kind &&
            r.Status == ContactRequestStatus.PendingAdminReview);

        if (duplicateExists)
            throw new BadRequestException("A pending request already exists for this profile.");
    }

    private async Task<List<ContactRequestDto>> ToDtosAsync(List<ContactRequest> requests, string viewerUserId, bool isAdmin = false)
    {
        var userIds = requests
            .SelectMany(r => new[] { r.RequesterUserId, r.TargetUserId })
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToList();

        var users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id.ToString(), u => u);

        return requests.Select(r => ToDto(r, users, viewerUserId, isAdmin)).ToList();
    }

    private static ContactRequestDto ToDto(
        ContactRequest request,
        IReadOnlyDictionary<string, User> users,
        string viewerUserId,
        bool isAdmin)
    {
        users.TryGetValue(request.RequesterUserId, out var requester);
        users.TryGetValue(request.TargetUserId, out var target);
        var revealContact = request.Status is ContactRequestStatus.Approved or ContactRequestStatus.Completed &&
            (isAdmin || CanAccess(request, viewerUserId));

        return new ContactRequestDto
        {
            Id = request.Id,
            RequesterUserId = request.RequesterUserId,
            RequesterName = ToDisplayName(requester),
            TargetUserId = request.TargetUserId,
            TargetName = ToDisplayName(target),
            TargetProfileType = request.TargetProfileType,
            Kind = request.Kind,
            Status = request.Status,
            Message = request.Message,
            PreferredCallbackAt = request.PreferredCallbackAt,
            ReviewedAt = request.ReviewedAt,
            ReviewNotes = request.ReviewNotes,
            RejectionReason = request.RejectionReason,
            CompletedAt = request.CompletedAt,
            ContactEmail = revealContact ? target?.Email : null,
            ContactPhone = revealContact ? target?.PhoneNumber : null,
            CreatedAt = request.CreatedAt
        };
    }

    private static bool CanAccess(ContactRequest request, string userId)
    {
        return string.Equals(request.RequesterUserId, userId, StringComparison.Ordinal) ||
            string.Equals(request.TargetUserId, userId, StringComparison.Ordinal);
    }

    private static string ToDisplayName(User? user)
    {
        if (user == null)
            return "Unknown user";

        return string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim())
            ? user.Email ?? user.Id.ToString()
            : $"{user.FirstName} {user.LastName}".Trim();
    }
}
