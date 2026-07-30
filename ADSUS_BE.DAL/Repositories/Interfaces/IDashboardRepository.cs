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
    /// Số liệu phát sinh trong khoảng thời gian đã chọn.
    /// <paramref name="toExclusive"/> là mốc LOẠI TRỪ, để ngày cuối được tính trọn vẹn.
    /// </summary>
    Task<ActivityCounts> GetActivityCountsAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);
}

/// <summary>Số đếm tài khoản. Bản ghi thuần số, không kèm dữ liệu cá nhân nào.</summary>
public record AccountCounts(
    int Total,
    int AdminCount,
    int DoctorCount,
    int NurseCount,
    int PatientCount,
    int ActiveCount,
    int LockedCount,
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
