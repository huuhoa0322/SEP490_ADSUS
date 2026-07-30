using ADSUS_BE.BLL.DashboardReporting.DTOs;

namespace ADSUS_BE.BLL.DashboardReporting.Interfaces;

/// <summary>
/// UC-05 FT-10 — thống kê vận hành hệ thống cho Admin (SCR-08).
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Tính toàn bộ số liệu cho khoảng thời gian đã chọn.
    ///
    /// Khoảng thời gian là TUỲ CHỌN. Không truyền thì mặc định 30 ngày gần nhất — UCS ghi rõ
    /// đây là giá trị tự đề xuất, PRD không quy định, cần chốt lại khi viết TDS/FDS.
    ///
    /// AF-01: khoảng thời gian không có dữ liệu thì trả về toàn số 0, KHÔNG báo lỗi.
    /// </summary>
    Task<DashboardStatisticsResponse> GetStatisticsAsync(
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken = default);
}
