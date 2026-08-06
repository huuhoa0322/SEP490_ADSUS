using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Các thao tác gần đây nhất, mới nhất lên đầu.
    ///
    /// Kèm luôn tên và vai trò của người thực hiện. Nếu chỉ trả về ActorId thì màn hình phải
    /// bắn thêm một lượt gọi cho mỗi dòng để tra tên — 10 dòng là 10 lượt đi về Supabase.
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Một dòng nhật ký đã ghép sẵn tên người thực hiện.
///
/// KHÔNG chứa ngày sinh hay bất kỳ dữ liệu y tế nào — xem chú thích ở nơi sinh ra phần
/// <c>Detail</c> (UC-04 BR-01).
/// </summary>
public record AuditLogEntry(
    Guid LogId,
    Guid ActorId,
    string ActorName,
    string ActorRole,
    string Action,
    string? Detail,
    DateTime PerformedAt);
