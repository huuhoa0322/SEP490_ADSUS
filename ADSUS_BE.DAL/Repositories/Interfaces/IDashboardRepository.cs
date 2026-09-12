namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// UC-05 FT-10 — số liệu vận hành cho màn thống kê (SCR-08).
///
/// Dashboard KHÔNG có bảng riêng: mọi con số đều đếm trực tiếp từ bảng của các module khác
/// tại thời điểm gọi (PRD §4.1.b ghi rõ đây là màn "derived", không lưu trữ gì).
///
/// BR-01: chỉ trả về SỐ ĐẾM đã tổng hợp. Không phương thức nào ở đây trả về tên, số điện
/// thoại hay bất kỳ thông tin nhận dạng nào của bệnh nhân.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>Đếm tài khoản theo vai trò và theo trạng thái. Không lọc theo thời gian.</summary>
    Task<AccountCounts> GetAccountCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Số liệu phát sinh trong khoảng thời gian đã chọn. Tính CẢ HAI đầu.
    ///
    /// Nhận ngày chứ không nhận mốc giờ, vì trong cùng một lượt đếm có hai loại cột: loại
    /// lưu ngày thuần (ngày khám, ngày mở khung giờ) và loại lưu mốc thời gian UTC (ngày tạo
    /// tài khoản). Ai gọi cũng chỉ có "ngày ở phòng khám", nên để repository tự quy đổi sang
    /// UTC cho từng cột — trước đây tầng trên quy đổi sẵn rồi truyền xuống, thành ra cột ngày
    /// thuần bị so với mốc UTC và lệch mất một ngày.
    /// </summary>
    Task<ActivityCounts> GetActivityCountsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-05 bước 3 — diễn biến theo từng ngày, để vẽ biểu đồ xu hướng.
    ///
    /// CHỈ trả về những ngày CÓ phát sinh. Tầng nghiệp vụ tự điền 0 vào các ngày trống —
    /// bắt database sinh ra một hàng cho mỗi ngày trong khoảng là việc thừa.
    /// </summary>
    Task<IReadOnlyList<DailyActivity>> GetDailyActivityAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Thống kê doanh thu phát sinh từ hóa đơn trong khoảng thời gian.</summary>
    Task<RevenueCounts> GetRevenueAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Top N thuốc được kê đơn nhiều nhất trong khoảng thời gian.</summary>
    Task<IReadOnlyList<TopMedicine>> GetTopPrescribedMedicinesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int topN = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>Số phát sinh của đúng một ngày.</summary>
public record DailyActivity(DateOnly Date, int NewAccounts, int Cases, int Appointments, decimal Revenue);

/// <summary>Tổng hợp doanh thu từ hóa đơn đã thanh toán và hóa đơn chờ.</summary>
public record RevenueCounts(
    decimal TotalRevenue,
    int PaidInvoiceCount,
    decimal CashRevenue,
    int CashCount,
    decimal BankTransferRevenue,
    int BankTransferCount,
    int PendingInvoiceCount,
    decimal PendingAmount);

/// <summary>Thông tin thuốc được kê đơn nhiều nhất.</summary>
public record TopMedicine(
    Guid MedicineId,
    string MedicineName,
    int PrescriptionCount,
    int TotalQuantityBase);

/// <summary>Số đếm tài khoản. Bản ghi thuần số, không kèm dữ liệu cá nhân nào.</summary>
public record AccountCounts(
    int Total,
    int AdminCount,
    int DoctorCount,
    int NurseCount,
    int PatientCount,
    int ActiveCount,
    int DeactivatedCount);

/// <summary>Số đếm hoạt động trong một khoảng thời gian.</summary>
public record ActivityCounts(
    int NewAccounts,
    int CaseCount,
    int AiRunCount,
    int AiConfirmedCount,
    int AiRejectedCount,
    int AiPendingCount,
    int AppointmentBookedCount,
    int AppointmentCancelledCount,
    int ScheduleSlotCount,
    int MedicationDoseCount,
    int MedicationTakenCount);
