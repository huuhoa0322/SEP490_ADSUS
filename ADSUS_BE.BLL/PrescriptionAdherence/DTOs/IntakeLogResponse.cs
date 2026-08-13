using ADSUS_BE.DAL.Entities;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Response DTO cho GET /me/medication-intakes (Patient xem lịch uống).
/// Status derive từ ConfirmedAt + ScheduledTime vs nowUtc (master convention Opt-X).
/// </summary>
public sealed record IntakeLogResponse(
    Guid IntakeId,
    Guid PrescriptionItemId,
    DateTime ScheduledTime,
    DateTime? ConfirmedAt,
    string Status);

public static class IntakeLogResponseMapper
{
    /// <summary>
    /// Derive status từ ConfirmedAt + ScheduledTime vs nowUtc.
    /// - ConfirmedAt has value  → TAKEN
    /// - ScheduledTime <= now   → OVERTIME (quá giờ, chưa xác nhận)
    /// - ScheduledTime > now   → PENDING (chưa đến giờ)
    /// </summary>
    public static IntakeLogResponse FromEntity(MedicationIntakeLog log, DateTime nowUtc)
        => new(
            log.IntakeId,
            log.PrescriptionItemId,
            log.ScheduledTime,
            log.ConfirmedAt,
            log.ConfirmedAt.HasValue
                ? AdherenceCalculator.StatusTaken
                : (log.ScheduledTime <= nowUtc
                    ? AdherenceCalculator.StatusOvertime
                    : AdherenceCalculator.StatusPending));
}