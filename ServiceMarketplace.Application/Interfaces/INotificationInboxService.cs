using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface INotificationInboxService
{
    Task<List<UserNotificationDto>> GetMineAsync(string userId, bool unreadOnly = false);
    Task<UserNotificationDto> MarkReadAsync(Guid notificationId, string userId);
    Task<int> MarkAllReadAsync(string userId);
}
