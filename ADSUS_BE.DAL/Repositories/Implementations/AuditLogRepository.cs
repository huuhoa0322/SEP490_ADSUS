using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Join sang bảng users ngay trong một truy vấn để lấy tên người thực hiện.
        // Sắp thêm theo LogId để thứ tự luôn xác định: nhiều thao tác trong cùng một lượt lưu
        // có PerformedAt giống hệt nhau tới từng tích, chỉ xếp theo thời gian là mỗi lần gọi
        // ra một thứ tự khác nhau.
        var rows = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.PerformedAt)
            .ThenByDescending(l => l.LogId)
            .Take(limit)
            .Select(l => new
            {
                l.LogId,
                l.ActorId,
                l.Actor.FullName,
                l.Actor.Role,
                l.Action,
                l.Detail,
                l.PerformedAt,
            })
            .ToListAsync(cancellationToken);

        // Đổi enum sang chuỗi SAU khi đã lấy dữ liệu về. Gọi ToString() ngay trong biểu thức
        // LINQ thì EF phải dịch nó xuống SQL — với enum gốc của PostgreSQL, chỗ đó build vẫn
        // qua nhưng chạy là văng.
        return rows
            .Select(r => new AuditLogEntry(
                r.LogId,
                r.ActorId,
                r.FullName,
                r.Role.ToString().ToUpperInvariant(),
                r.Action,
                r.Detail,
                r.PerformedAt))
            .ToList();
    }

    public async Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        string? action,
        string? actorRole,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(l =>
                (l.Actor != null && l.Actor.FullName != null && l.Actor.FullName.ToLower().Contains(k)) ||
                (l.Actor != null && l.Actor.Phone != null && l.Actor.Phone.Contains(k)) ||
                (l.Action != null && l.Action.ToLower().Contains(k)) ||
                (l.Detail != null && l.Detail.ToLower().Contains(k)));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim().ToUpperInvariant();
            if (act == "ACCOUNT")
            {
                var accountActions = new[] { "CREATE_ACCOUNT", "UPDATE_ACCOUNT", "DEACTIVATE_ACCOUNT", "REACTIVATE_ACCOUNT", "ACCOUNT_LOCK", "ACCOUNT_UNLOCK", "ADMIN_RESET_PASSWORD", "SELF_RESET_PASSWORD" };
                query = query.Where(l => accountActions.Contains(l.Action));
            }
            else if (act == "AI_MODEL")
            {
                var aiActions = new[] { "REGISTER_AI_MODEL", "UPDATE_AI_MODEL", "ACTIVATE_AI_MODEL" };
                query = query.Where(l => aiActions.Contains(l.Action));
            }
            else if (act == "NURSE_PATIENT")
            {
                var nurseActions = new[] { "NURSE_CREATE_PATIENT_ACCOUNT", "NURSE_UPDATE_PATIENT_ACCOUNT", "NURSE_RESET_PATIENT_PASSWORD" };
                query = query.Where(l => nurseActions.Contains(l.Action));
            }
            else
            {
                query = query.Where(l => l.Action == act || l.Action.ToUpper().StartsWith(act));
            }
        }

        if (!string.IsNullOrWhiteSpace(actorRole) && Enum.TryParse<UserRole>(actorRole.Trim(), true, out var roleEnum))
        {
            query = query.Where(l => l.Actor != null && l.Actor.Role == roleEnum);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.PerformedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value;
            if (end.TimeOfDay == TimeSpan.Zero)
            {
                end = end.AddDays(1).AddTicks(-1);
            }
            query = query.Where(l => l.PerformedAt <= end);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize is < 1 or > 1000 ? 15 : pageSize;

        var rows = await query
            .OrderByDescending(l => l.PerformedAt)
            .ThenByDescending(l => l.LogId)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(l => new
            {
                l.LogId,
                l.ActorId,
                ActorFullName = l.Actor != null ? l.Actor.FullName : string.Empty,
                ActorRole = l.Actor != null ? (UserRole?)l.Actor.Role : null,
                l.Action,
                l.Detail,
                l.PerformedAt,
            })
            .ToListAsync(cancellationToken);

        var entries = rows
            .Select(r => new AuditLogEntry(
                r.LogId,
                r.ActorId,
                r.ActorFullName,
                r.ActorRole.HasValue ? r.ActorRole.Value.ToString().ToUpperInvariant() : string.Empty,
                r.Action,
                r.Detail,
                r.PerformedAt))
            .ToList();

        return (entries, totalCount);
    }
}
