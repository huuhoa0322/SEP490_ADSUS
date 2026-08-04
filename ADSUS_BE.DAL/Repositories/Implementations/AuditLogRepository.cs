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
}
