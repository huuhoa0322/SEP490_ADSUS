using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface INotificationLogRepository
{
    Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationLog>> GetByUserIdAsync(Guid userId, int page, int pageSize, bool includeDeleted = false, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid logId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid logId, CancellationToken ct = default);
}
