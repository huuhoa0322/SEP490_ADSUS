using System.Globalization;
using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.BLL.DashboardReporting.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.DashboardReporting.Services;

/// <summary>
/// UC-05 FT-10 — tổng hợp số liệu vận hành cho SCR-08.
/// </summary>
public class DashboardService : IDashboardService
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Khoảng mặc định khi Admin chưa chọn gì, tính CẢ ngày hôm nay.
    /// UCS ghi rõ đây là giá trị TỰ ĐỀ XUẤT, PRD không quy định — cần chốt khi viết TDS/FDS.
    /// </summary>
    private const int DefaultRangeDays = 30;

    /// <summary>
    /// Chặn khoảng quá dài để một cú bấm nhầm không quét cả bảng nhiều năm.
    /// Cũng tính cả hai đầu: 366 nghĩa là nhiều nhất 366 điểm trên biểu đồ.
    /// </summary>
    private const int MaxRangeDays = 366;

    private readonly IDashboardRepository _dashboard;
    private readonly IAiModelVersionRepository _aiModelRepo;

    public DashboardService(IDashboardRepository dashboard, IAiModelVersionRepository aiModelRepo)
    {
        _dashboard = dashboard;
        _aiModelRepo = aiModelRepo;
    }

    public async Task<DashboardStatisticsResponse> GetStatisticsAsync(
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(fromDate, toDate);

        // Truyền thẳng khoảng NGÀY xuống. Repository tự quy đổi sang UTC cho từng cột, vì
        // chỉ ở đó mới biết cột nào lưu mốc giờ và cột nào lưu ngày thuần — xem ClinicClock.
        var accounts = await _dashboard.GetAccountCountsAsync(cancellationToken);
        var activity = await _dashboard.GetActivityCountsAsync(from, to, cancellationToken);
        var daily = await _dashboard.GetDailyActivityAsync(from, to, cancellationToken);
        var activeModel = await _aiModelRepo.GetActiveVersionAsync(cancellationToken);

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
            },

            Adherence = new AdherenceStatistics
            {
                ScheduledDoseCount = activity.MedicationDoseCount,
                TakenDoseCount = activity.MedicationTakenCount,
                AdherenceRate = Percent(activity.MedicationTakenCount, activity.MedicationDoseCount),
            },

            ActiveAiModel = activeModel == null ? new AiModelMetrics() : new AiModelMetrics
            {
                VersionCode = activeModel.VersionCode,
                Precision = (activeModel.LiveTp + activeModel.LiveFp) > 0 ? (decimal)activeModel.LiveTp / (activeModel.LiveTp + activeModel.LiveFp) : null,
                Recall = (activeModel.LiveTp + activeModel.LiveFn) > 0 ? (decimal)activeModel.LiveTp / (activeModel.LiveTp + activeModel.LiveFn) : null,
                Map50 = activeModel.LiveMap50,
                LastEvaluatedAt = activeModel.LastEvaluatedAt
            },

            Trend = BuildTrend(from, to, daily),
        };
    }

    /// <summary>
    /// Trải kết quả thưa của repository thành dãy liên tục, mỗi ngày một điểm.
    ///
    /// Repository chỉ trả về ngày CÓ phát sinh. Đưa thẳng dãy thưa đó lên biểu đồ thì đường
    /// nối thẳng qua các ngày trống, nhìn như hoạt động vẫn đều trong khi thực tế là không
    /// có gì — đọc sai hẳn ý nghĩa.
    /// </summary>
    private static List<DailyPoint> BuildTrend(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<DailyActivity> daily)
    {
        // Tra theo từ điển thay vì quét lại danh sách cho từng ngày: khoảng tối đa 366 ngày,
        // quét lồng nhau là hơn 130 nghìn phép so sánh không cần thiết.
        var theoNgay = daily.ToDictionary(d => d.Date);
        var diem = new List<DailyPoint>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            theoNgay.TryGetValue(date, out var d);

            diem.Add(new DailyPoint
            {
                Date = date.ToString(DateFormat, CultureInfo.InvariantCulture),
                NewAccounts = d?.NewAccounts ?? 0,
                Cases = d?.Cases ?? 0,
                Appointments = d?.Appointments ?? 0,
            });
        }

        return diem;
    }

    /// <summary>
    /// Đọc và làm sạch khoảng thời gian. Dữ liệu vào luôn được nắn về một khoảng hợp lệ,
    /// không bao giờ ném lỗi — AF-01 yêu cầu màn này không được vỡ.
    /// </summary>
    private static (DateOnly From, DateOnly To) ResolveRange(string? fromDate, string? toDate)
    {
        var today = ClinicClock.Today();

        var to = ParseOrNull(toDate) ?? today;

        // Trừ đi (N - 1) chứ không phải N: khoảng tính cả hai đầu, nên "30 ngày" phải ra
        // đúng 30 điểm. Trừ thẳng 30 là ra 31 ngày — biểu đồ dài hơn nhãn ghi trên nút.
        var from = ParseOrNull(fromDate) ?? to.AddDays(-(DefaultRangeDays - 1));

        // Người dùng chọn ngược thì đổi chỗ, thay vì trả về bảng trống khó hiểu.
        if (from > to) (from, to) = (to, from);

        // Cũng tính cả hai đầu: chênh lệch 366 ngày là 367 điểm, quá giới hạn một ngày.
        if (to.DayNumber - from.DayNumber >= MaxRangeDays)
        {
            from = to.AddDays(-(MaxRangeDays - 1));
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
}
