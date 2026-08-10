using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>Đọc nhật ký thao tác cho màn hình quản trị.</summary>
public class AuditLogService : IAuditLogService
{
    /// <summary>Mặc định khi màn hình không nói rõ muốn lấy bao nhiêu.</summary>
    private const int DefaultLimit = 10;

    /// <summary>
    /// Trần cứng. Không có nó thì một lời gọi <c>?limit=100000</c> kéo cả bảng nhật ký về —
    /// bảng này chỉ có lớn dần theo thời gian chứ không bao giờ nhỏ đi.
    /// </summary>
    private const int MaxLimit = 100;

    private readonly IAuditLogRepository _auditLogs;

    public AuditLogService(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<IReadOnlyList<AuditLogResponse>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var soDong = limit is < 1 or > MaxLimit ? DefaultLimit : limit;

        var entries = await _auditLogs.GetRecentAsync(soDong, cancellationToken);

        return entries
            .Select(e => new AuditLogResponse
            {
                LogId = e.LogId,
                ActorId = e.ActorId,
                ActorName = e.ActorName,
                ActorRole = e.ActorRole,
                Action = e.Action,
                Detail = e.Detail,
                PerformedAt = e.PerformedAt,
            })
            .ToList();
    }
}
