using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// Nhật ký thao tác quản trị — ai đã làm gì, lúc nào.
///
/// CHỈ ADMIN, và CHỈ ĐỌC. Không có endpoint sửa hay xoá: nhật ký mà sửa được thì mất sạch giá
/// trị làm bằng chứng, người gây chuyện chỉ việc xoá dấu vết của mình rồi chối.
///
/// Hiện ghi lại các thao tác của UC-04 (tạo, sửa, khoá, mở khoá, vô hiệu hoá tài khoản),
/// UC-03 (cấp lại mật khẩu) và Module 6 (quản lý phiên bản mô hình AI). Cùng dùng chung một
/// bảng nên module nào ghi vào cũng hiện ở đây.
/// </summary>
[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Roles = "ADMIN")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogs;

    public AuditLogsController(IAuditLogService auditLogs) => _auditLogs = auditLogs;

    /// <summary>
    /// Danh sách nhật ký thao tác: hỗ trợ phân trang (15 dòng/trang), tìm kiếm realtime (keyword/search),
    /// lọc theo hành động/category (action), vai trò (role/actorRole), và khoảng thời gian (fromDate, toDate).
    /// Duy trì tương thích ngược cho Dashboard khi gọi với limit mà không truyền page.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? search = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? action = null,
        [FromQuery] string? role = null,
        [FromQuery] string? actorRole = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? limit = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        // Tương thích ngược: Dashboard (SCR-08) gọi với limit (ví dụ ?limit=10) mà không truyền page
        if (limit.HasValue && !Request.Query.ContainsKey("page"))
        {
            var recent = await _auditLogs.GetRecentAsync(limit.Value, cancellationToken);
            return Ok(ApiResponse<IReadOnlyList<AuditLogResponse>>.Ok(recent, "Audit log loaded."));
        }

        var effectiveKeyword = search ?? keyword;
        var effectiveRole = role ?? actorRole;

        var result = await _auditLogs.GetPagedAsync(
            effectiveKeyword, action, effectiveRole, fromDate, toDate, page, pageSize, cancellationToken);

        return Ok(ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>.Ok(result, "Audit log loaded."));
    }
}
