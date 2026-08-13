using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho MedicationIntakeLog (mỗi liều thuốc 1 dòng). Status "PENDING" /
/// "TAKEN" / "OVERTIME" được derive từ ConfirmedAt + ScheduledTime vs now tại tầng
/// API (master convention, xem IntakeLogResponseMapper). Không filter/derive status
/// trong repo. Idempotency nhờ UNIQUE constraint trên (prescription_item_id,
/// scheduled_time) tầng DB — handler bắt PostgresException 23505 để skip duplicate
/// khi Quartz re-fire job.
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

    /// <summary>Lấy log theo ID, có Include PrescriptionItem → Prescription → Case.</summary>
    Task<MedicationIntakeLog?> GetByIdAsync(Guid intakeId, CancellationToken ct = default);

    /// <summary>Lấy logs của 1 đơn, kèm medicine info, sắp xếp theo scheduled_time.</summary>
    Task<IReadOnlyList<MedicationIntakeLog>> ListByPrescriptionAsync(
        Guid prescriptionId,
        CancellationToken ct = default);

    /// <summary>Lấy logs hôm nay (UTC 00:00 → 00:00 ngày mai) của 1 bệnh nhân. Không filter ConfirmedAt — FE derive status.</summary>
    Task<IReadOnlyList<MedicationIntakeLog>> ListUpcomingAsync(
        Guid patientProfileId,
        CancellationToken ct = default);

    /// <summary>Xác nhận đã uống (GB-01: ghi ConfirmedAt = now).</summary>
    Task ConfirmTakenAsync(Guid intakeId, DateTime confirmedAt, CancellationToken ct = default);

    /// <summary>
    /// JOB-01: lấy pending logs sẵn sàng nhắc.
    /// - ConfirmedAt == null (chưa uống).
    /// - ScheduledTime trong khoảng [now - reminderWindowMinutes, now] (gửi 1 lần khi vừa đến giờ).
    /// - Không cần LastReminderSentAt column — dùng time window thay thế.
    /// </summary>
    Task<IReadOnlyList<MedicationIntakeLog>> ListDueRemindersAsync(
        DateTime windowStart,
        int reminderWindowMinutes,
        CancellationToken ct = default);
}
