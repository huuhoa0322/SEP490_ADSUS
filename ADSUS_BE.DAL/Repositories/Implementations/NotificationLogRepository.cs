using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class NotificationLogRepository : INotificationLogRepository
{
    private readonly AppDbContext _db;

    public NotificationLogRepository(AppDbContext db) => _db = db;

    public async Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken ct = default)
    {
        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IReadOnlyList<NotificationLog>> GetByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = _db.NotificationLogs.AsNoTracking();

        if (!includeDeleted)
        {
            query = query.Where(n => n.IsDeleted != true);
        }

        return await query
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.NotificationLogs
            .CountAsync(n => n.UserId == userId
                             && n.IsDeleted != true
                             && n.ReadAt == null,
                ct);
    }

    public async Task MarkAsReadAsync(Guid logId, CancellationToken ct = default)
    {
        var log = await _db.NotificationLogs
            .FirstOrDefaultAsync(n => n.LogId == logId, ct);

        if (log is not null)
        {
            log.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _db.NotificationLogs
            .Where(n => n.UserId == userId && n.IsDeleted != true && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task DeleteAsync(Guid logId, CancellationToken ct = default)
    {
        var log = await _db.NotificationLogs
            .FirstOrDefaultAsync(n => n.LogId == logId, ct);

        if (log is not null)
        {
            log.IsDeleted = true;
            log.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
