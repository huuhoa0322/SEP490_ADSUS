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
    /// Các thao tác gần đây nhất, mới nhất lên đầu.
    ///
    /// Dashboard (SCR-08) gọi với limit mặc định là 10. Tham số để sẵn cho màn nhật ký đầy đủ
    /// sau này — lúc đó chỉ cần thêm phân trang, không phải viết lại endpoint.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditLogResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogs.GetRecentAsync(limit, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<AuditLogResponse>>.Ok(logs, "Audit log loaded."));
    }
}
