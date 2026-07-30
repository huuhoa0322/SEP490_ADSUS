using System.Globalization;
using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.BLL.DashboardReporting.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.DashboardReporting.Services;

/// <summary>
/// UC-05 FT-10 — tổng hợp số liệu vận hành cho SCR-08.
/// </summary>
public class DashboardService : IDashboardService
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Khoảng mặc định khi Admin chưa chọn gì.
    /// UCS ghi rõ đây là giá trị TỰ ĐỀ XUẤT, PRD không quy định — cần chốt khi viết TDS/FDS.
    /// </summary>
    private const int DefaultRangeDays = 30;

    /// <summary>Chặn khoảng quá dài để một cú bấm nhầm không quét cả bảng nhiều năm.</summary>
    private const int MaxRangeDays = 366;

    private readonly IDashboardRepository _dashboard;

    public DashboardService(IDashboardRepository dashboard) => _dashboard = dashboard;

    public async Task<DashboardStatisticsResponse> GetStatisticsAsync(
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(fromDate, toDate);

        // Mốc kết thúc cộng thêm một ngày rồi so sánh "nhỏ hơn", để ngày cuối được tính trọn
        // vẹn. Dùng "nhỏ hơn hoặc bằng" với DateTime là mất sạch dữ liệu phát sinh trong
        // ngày cuối, vì mọi mốc giờ đều lớn hơn 00:00.
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var accounts = await _dashboard.GetAccountCountsAsync(cancellationToken);
        var activity = await _dashboard.GetActivityCountsAsync(fromUtc, toExclusiveUtc, cancellationToken);

        return new DashboardStatisticsResponse
        {
            FromDate = from.ToString(DateFormat, CultureInfo.InvariantCulture),
            ToDate = to.ToString(DateFormat, CultureInfo.InvariantCulture),

            Accounts = new AccountStatistics
            {
                Total = accounts.Total,
                AdminCount = accounts.AdminCount,
                DoctorCount = accounts.DoctorCount,
                NurseCount = accounts.NurseCount,
                PatientCount = accounts.PatientCount,
                ActiveCount = accounts.ActiveCount,
                LockedCount = accounts.LockedCount,
                DeactivatedCount = accounts.DeactivatedCount,
                NewInRange = activity.NewAccounts,
                ActiveRate = Percent(accounts.ActiveCount, accounts.Total),
            },

            Clinical = new ClinicalStatistics
            {
                CaseCount = activity.CaseCount,
                AiRunCount = activity.AiRunCount,
                AiConfirmedCount = activity.AiConfirmedCount,
                AiRejectedCount = activity.AiRejectedCount,
                AiPendingCount = activity.AiPendingCount,
                // Mẫu số chỉ gồm những kết quả bác sĩ đã duyệt — xem chú thích ở DTO.
                AiConfirmRate = Percent(
                    activity.AiConfirmedCount,
                    activity.AiConfirmedCount + activity.AiRejectedCount),
            },

            Appointments = new AppointmentStatistics
            {
                BookedCount = activity.AppointmentBookedCount,
                CancelledCount = activity.AppointmentCancelledCount,
                SlotCount = activity.ScheduleSlotCount,
                CancellationRate = Percent(
                    activity.AppointmentCancelledCount,
                    activity.AppointmentBookedCount + activity.AppointmentCancelledCount),
                AverageBookingsPerSlot = Ratio(
                    activity.AppointmentBookedCount + activity.AppointmentCancelledCount,
                    activity.ScheduleSlotCount),
            },

            Adherence = new AdherenceStatistics
            {
                ScheduledDoseCount = activity.MedicationDoseCount,
                TakenDoseCount = activity.MedicationTakenCount,
                AdherenceRate = Percent(activity.MedicationTakenCount, activity.MedicationDoseCount),
            },
        };
    }

    /// <summary>
    /// Đọc và làm sạch khoảng thời gian. Dữ liệu vào luôn được nắn về một khoảng hợp lệ,
    /// không bao giờ ném lỗi — AF-01 yêu cầu màn này không được vỡ.
    /// </summary>
    private static (DateOnly From, DateOnly To) ResolveRange(string? fromDate, string? toDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var to = ParseOrNull(toDate) ?? today;
        var from = ParseOrNull(fromDate) ?? to.AddDays(-DefaultRangeDays);

        // Người dùng chọn ngược thì đổi chỗ, thay vì trả về bảng trống khó hiểu.
        if (from > to) (from, to) = (to, from);

        if (to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            from = to.AddDays(-MaxRangeDays);
        }

        return (from, to);
    }

    private static DateOnly? ParseOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>Phần trăm, làm tròn 1 chữ số. Mẫu số 0 thì trả 0 — AF-01, không chia cho 0.</summary>
    private static double Percent(int part, int total) =>
        total == 0 ? 0 : Math.Round(part * 100.0 / total, 1);

    /// <summary>Tỉ số trung bình, làm tròn 2 chữ số. Mẫu số 0 thì trả 0.</summary>
    private static double Ratio(int part, int total) =>
        total == 0 ? 0 : Math.Round(part / (double)total, 2);
}
