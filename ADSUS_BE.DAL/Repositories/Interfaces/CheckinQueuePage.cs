using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Một trang hàng chờ check-in kèm số lượng theo từng trạng thái. Các con số đếm trên TOÀN BỘ
/// khoảng ngày + từ khoá đã chọn, không phụ thuộc bộ lọc trạng thái hay phân trang.
/// </summary>
public sealed record CheckinQueuePage(
    IReadOnlyList<Appointment> Items,
    int TotalCount,
    int BookedCount,
    int CheckedInCount,
    int CancelledCount,
    int NoShowCount);
