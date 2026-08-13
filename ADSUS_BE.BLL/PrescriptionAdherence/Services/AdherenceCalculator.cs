using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// Tính tỉ lệ tuân thủ (FT-27) cho 1 PrescriptionItem dựa trên danh sách intake logs.
/// Status = "PENDING" / "TAKEN" derive từ ConfirmedAt — không có column status ở DB
/// (master convention, xem AppDbContext.OnModelCreating).
///
/// Quy ước:
///   - Adherence = (số liều TAKEN) / (tổng số liều đã đến hạn) * 100
///   - Nếu chưa có liều nào đến hạn → 0
///   - Nếu tất cả liều TAKEN → 100
///   - Nếu tất cả liều PENDING → 0
///
/// Lưu ý: Chỉ tính trên logs có ScheduledTime &lt;= now (UTC). Liều tương lai
/// chưa đến hạn không tính — tránh adherence giảm do "chưa uống".
/// </summary>
public static class AdherenceCalculator
{
    /// <summary>Status constants — derive từ ConfirmedAt + ScheduledTime vs nowUtc (master convention Opt-X).</summary>
    public const string StatusPending = "PENDING";
    public const string StatusTaken = "TAKEN";
    public const string StatusOvertime = "OVERTIME";

    /// <summary>Tính tỉ lệ tuân thủ (% 0..100, làm tròn 2 chữ số).</summary>
    /// <param name="logs">Tất cả intake logs của 1 PrescriptionItem (bất kỳ order).</param>
    /// <param name="nowUtc">Mốc "hiện tại" UTC. Tham số để test deterministic.</param>
    public static decimal Calculate(IReadOnlyCollection<MedicationIntakeLog> logs, DateTime nowUtc)
    {
        if (logs is null || logs.Count == 0) return 0m;

        var dueLogs = logs.Where(l => l.ScheduledTime <= nowUtc).ToList();
        if (dueLogs.Count == 0) return 0m;

        var takenCount = dueLogs.Count(l => l.ConfirmedAt.HasValue);
        var ratio = (decimal)takenCount / dueLogs.Count;
        return Math.Round(ratio * 100m, 2);
    }

    /// <summary>Trả về status string (TAKEN / PENDING) từ ConfirmedAt. Dùng cho AdherenceCalculator.</summary>
    public static string StatusOf(MedicationIntakeLog log)
        => log.ConfirmedAt.HasValue ? StatusTaken : StatusPending;
}