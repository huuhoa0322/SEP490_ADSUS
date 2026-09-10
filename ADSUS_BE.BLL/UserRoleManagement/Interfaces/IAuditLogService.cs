using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// Đọc nhật ký thao tác quản trị.
///
/// Chỉ có ĐỌC. Nhật ký không sửa và không xoá được qua API — nhật ký mà sửa được thì mất
/// sạch giá trị làm bằng chứng, người gây chuyện chỉ việc xoá dấu vết của mình.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Các thao tác gần đây nhất, mới nhất lên đầu.
    /// </summary>
    /// <param name="limit">Số dòng muốn lấy. Giá trị vô lý sẽ bị nắn về khoảng hợp lệ.</param>
    Task<IReadOnlyList<AuditLogResponse>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm và phân trang nhật ký thao tác theo từ khóa, hành động, vai trò và khoảng thời gian.
    /// </summary>
    Task<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>> GetPagedAsync(
        string? keyword,
        string? action,
        string? actorRole,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 15,
        CancellationToken cancellationToken = default);
}

