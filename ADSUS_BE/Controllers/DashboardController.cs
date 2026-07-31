using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.BLL.DashboardReporting.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-05 FT-10 — thống kê vận hành hệ thống (SCR-08).
///
/// CHỈ ADMIN (BR-03). Bảng quyền PRD §3.2 ghi "Statistics dashboard | View" là Full cho
/// Admin, No cho Doctor/Nurse và Patient.
///
/// BR-02: chỉ đọc — ở đây không có phương thức nào ghi dữ liệu.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = "ADMIN")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>
    /// Số liệu tổng hợp cho khoảng thời gian đã chọn.
    ///
    /// Hai tham số đều tuỳ chọn, định dạng yyyy-MM-dd. Không truyền thì lấy 30 ngày gần nhất.
    /// Giá trị sai định dạng được bỏ qua và rơi về mặc định, không báo lỗi — AF-01 yêu cầu
    /// màn này không bao giờ vỡ, kể cả khi không có dữ liệu nào.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatisticsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        CancellationToken cancellationToken)
    {
        var statistics = await _dashboard.GetStatisticsAsync(fromDate, toDate, cancellationToken);

        return Ok(ApiResponse<DashboardStatisticsResponse>.Ok(statistics, "Statistics loaded."));
    }
}
