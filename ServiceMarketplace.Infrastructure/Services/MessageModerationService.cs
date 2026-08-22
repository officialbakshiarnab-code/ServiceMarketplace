using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class MessageModerationService(AppDbContext context) : IMessageModerationService
{
    private const int DefaultRetentionMonthsAfterClosure = 12;

    public async Task<MessageReportDto> ReportAsync(
        Guid conversationId,
        Guid messageId,
        string reporterUserId,
        CreateMessageReportDto request)
    {
        if (string.IsNullOrWhiteSpace(reporterUserId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var participantExists = await context.ConversationParticipants.AnyAsync(p =>
            p.ConversationId == conversationId &&
            p.UserId == reporterUserId &&
            p.LeftAt == null);

        if (!participantExists)
            throw new NotFoundException("Conversation not found.");

        var message = await context.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId)
            ?? throw new NotFoundException("Message not found.");

        if (message.Type == MessageType.System)
            throw new BadRequestException("System messages cannot be reported.");

        if (string.Equals(message.SenderUserId, reporterUserId, StringComparison.Ordinal))
            throw new BadRequestException("You cannot report your own message.");

        var duplicateExists = await context.MessageReports.AnyAsync(r =>
            r.MessageId == messageId &&
            r.ReporterUserId == reporterUserId &&
            r.Status == MessageReportStatus.PendingReview);

        if (duplicateExists)
            throw new BadRequestException("You already have a pending report for this message.");

        var report = new MessageReport
        {
            ConversationId = conversationId,
            MessageId = messageId,
            ReporterUserId = reporterUserId,
            ReportedSenderUserId = message.SenderUserId,
            Reason = NormalizeRequired(request.Reason, 120, "Report reason is required."),
            Details = NormalizeOptional(request.Details, 1000),
            Status = MessageReportStatus.PendingReview,
            CreatedAt = DateTime.UtcNow
        };

        context.MessageReports.Add(report);
        await context.SaveChangesAsync();

        return await GetDtoAsync(report.Id);
    }

    public async Task<List<MessageReportDto>> GetAdminQueueAsync(MessageReportStatus? status = null)
    {
        var query = context.MessageReports
            .AsNoTracking()
            .Include(r => r.Conversation)
            .Include(r => r.Message)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var reports = await query
            .OrderBy(r => r.Status == MessageReportStatus.PendingReview ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync();

        return await ToDtosAsync(reports);
    }

    public async Task<MessageReportDto> ReviewAsync(Guid reportId, string adminUserId, ReviewMessageReportDto request)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        if (request.Status == MessageReportStatus.PendingReview)
            throw new BadRequestException("Admin review must resolve the report.");

        if ((request.HideMessage || request.RestrictConversation || request.LegalHoldUntil.HasValue) &&
            request.Status != MessageReportStatus.ActionTaken)
        {
            throw new BadRequestException("Moderation actions require Action Taken status.");
        }

        if (request.LegalHoldUntil.HasValue && request.LegalHoldUntil.Value <= DateTime.UtcNow)
            throw new BadRequestException("Legal hold date must be in the future.");

        var report = await context.MessageReports
            .Include(r => r.Message)
            .Include(r => r.Conversation)
            .FirstOrDefaultAsync(r => r.Id == reportId)
            ?? throw new NotFoundException("Message report not found.");

        report.Status = request.Status;
        report.ReviewedByUserId = adminUserId;
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewNotes = NormalizeOptional(request.ReviewNotes, 1000);
        report.MessageHidden = request.HideMessage;
        report.ConversationRestricted = request.RestrictConversation;
        report.LegalHoldUntil = request.LegalHoldUntil;
        report.UpdatedAt = DateTime.UtcNow;

        if (request.HideMessage)
        {
            report.Message.IsHidden = true;
            report.Message.HiddenByUserId = adminUserId;
            report.Message.HiddenAt = DateTime.UtcNow;
            report.Message.HiddenReason = NormalizeOptional(request.ReviewNotes, 1000) ?? "Hidden after admin moderation review.";
            report.Message.UpdatedAt = DateTime.UtcNow;
        }

        if (request.RestrictConversation)
        {
            report.Conversation.Status = ConversationStatus.Restricted;
            report.Conversation.ClosedAt ??= DateTime.UtcNow;
            report.Conversation.RetainUntil = CalculateRetainUntil(report.Conversation.ClosedAt.Value, request.LegalHoldUntil);
            report.Conversation.LegalHoldUntil = request.LegalHoldUntil;
            report.Conversation.RetentionNotes = NormalizeOptional(request.ReviewNotes, 1000) ?? "Restricted after admin moderation review.";
            report.Conversation.UpdatedAt = DateTime.UtcNow;
        }
        else if (request.LegalHoldUntil.HasValue)
        {
            report.Conversation.LegalHoldUntil = request.LegalHoldUntil;
            report.Conversation.RetainUntil = CalculateRetainUntil(DateTime.UtcNow, request.LegalHoldUntil);
            report.Conversation.RetentionNotes = NormalizeOptional(request.ReviewNotes, 1000) ?? "Legal hold set after admin moderation review.";
            report.Conversation.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return await GetDtoAsync(report.Id);
    }

    private async Task<MessageReportDto> GetDtoAsync(Guid reportId)
    {
        var report = await context.MessageReports
            .AsNoTracking()
            .Include(r => r.Conversation)
            .Include(r => r.Message)
            .FirstOrDefaultAsync(r => r.Id == reportId)
            ?? throw new NotFoundException("Message report not found.");

        return (await ToDtosAsync([report])).Single();
    }

    private async Task<List<MessageReportDto>> ToDtosAsync(List<MessageReport> reports)
    {
        var userIds = reports
            .SelectMany(r => new[] { r.ReporterUserId, r.ReportedSenderUserId })
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToList();

        var names = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id.ToString(), u => ToDisplayName(u));

        return reports.Select(r => new MessageReportDto
        {
            Id = r.Id,
            ConversationId = r.ConversationId,
            MessageId = r.MessageId,
            ConversationSubject = r.Conversation.Subject,
            ReporterUserId = r.ReporterUserId,
            ReporterName = names.GetValueOrDefault(r.ReporterUserId, "Marketplace participant"),
            ReportedSenderUserId = r.ReportedSenderUserId,
            ReportedSenderName = names.GetValueOrDefault(r.ReportedSenderUserId, "Marketplace participant"),
            Reason = r.Reason,
            Details = r.Details,
            MessagePreview = ToPreview(r.Message.Body),
            Status = r.Status,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedAt = r.ReviewedAt,
            ReviewNotes = r.ReviewNotes,
            MessageHidden = r.MessageHidden,
            ConversationRestricted = r.ConversationRestricted,
            LegalHoldUntil = r.LegalHoldUntil,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    private static string NormalizeRequired(string value, int maxLength, string requiredMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException(requiredMessage);

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string ToPreview(string body)
    {
        return body.Length <= 180 ? body : body[..180] + "...";
    }

    private static string ToDisplayName(User user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name)
            ? user.Email ?? "Marketplace participant"
            : name;
    }

    private static DateTime CalculateRetainUntil(DateTime closedAt, DateTime? legalHoldUntil)
    {
        var defaultRetainUntil = closedAt.AddMonths(DefaultRetentionMonthsAfterClosure);
        return legalHoldUntil.HasValue && legalHoldUntil.Value > defaultRetainUntil
            ? legalHoldUntil.Value
            : defaultRetainUntil;
    }
}
