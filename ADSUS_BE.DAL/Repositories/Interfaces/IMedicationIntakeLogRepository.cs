using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho MedicationIntakeLog (mỗi liều thuốc 1 dòng). Status = "PENDING" /
/// "TAKEN" được derive từ ConfirmedAt — KHÔNG có column status (master convention,
/// xem AdsusDbContext AppDbContext.OnModelCreating). Idempotency nhờ UNIQUE constraint
/// trên (prescription_item_id, scheduled_time) tầng DB — handler có thể bắt
/// PostgresException 23505 để skip duplicate khi Quartz re-fire job.
/// </summary>
public interface IMedicationIntakeLogRepository
{
    /// <summary>Tìm log theo (item, scheduled_time). Trả null nếu chưa có.</summary>
    Task<MedicationIntakeLog?> FindByItemAndTimeAsync(
        Guid prescriptionItemId,
        DateTime scheduledTimeUtc,
        CancellationToken ct = default);

    /// <summary>Lấy toàn bộ logs của 1 item, sắp xếp theo thời gian uống tăng dần.</summary>
    Task<IReadOnlyList<MedicationIntakeLog>> ListByItemAsync(
        Guid prescriptionItemId,
        CancellationToken ct = default);

    /// <summary>Lấy logs của 1 bệnh nhân trong khoảng thời gian (UTC). Dùng cho report.</summary>
    Task<IReadOnlyList<MedicationIntakeLog>> ListByPatientRangeAsync(
        Guid patientId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>Add 1 log vào change tracker.</summary>
    Task AddAsync(MedicationIntakeLog log, CancellationToken ct = default);

    /// <summary>Add nhiều logs cùng lúc (dùng cho IntakeLogGenerationService khi sinh lịch).</summary>
    Task AddRangeAsync(IEnumerable<MedicationIntakeLog> logs, CancellationToken ct = default);
}
