using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface IMessageModerationService
{
    Task<MessageReportDto> ReportAsync(Guid conversationId, Guid messageId, string reporterUserId, CreateMessageReportDto request);
    Task<List<MessageReportDto>> GetAdminQueueAsync(MessageReportStatus? status = null);
    Task<MessageReportDto> ReviewAsync(Guid reportId, string adminUserId, ReviewMessageReportDto request);
}
