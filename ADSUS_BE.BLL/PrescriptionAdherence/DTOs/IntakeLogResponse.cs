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
    string Status,
    string MedicineName,
    string Dosage,
    string? Instructions);

public static class IntakeLogResponseMapper
{
    /// <summary>
    /// Derive status từ ConfirmedAt + ScheduledTime vs nowUtc.
    /// Thứ tự kiểm tra: TAKEN → MISSED → OVERTIME → PENDING (từ cuối chuỗi ngược lại).
    /// </summary>
    public static IntakeLogResponse FromEntity(MedicationIntakeLog log, DateTime nowUtc)
    {
        var status = log.ConfirmedAt.HasValue
            ? AdherenceCalculator.StatusTaken
            : (log.ScheduledTime.AddHours(2) <= nowUtc
                ? AdherenceCalculator.StatusMissed
                : (log.ScheduledTime <= nowUtc
                    ? AdherenceCalculator.StatusOvertime
                    : AdherenceCalculator.StatusPending));

        return new IntakeLogResponse(
            log.IntakeId,
            log.PrescriptionItemId,
            log.ScheduledTime,
            log.ConfirmedAt,
            status,
            log.PrescriptionItem?.Medicine?.Name ?? string.Empty,
            log.PrescriptionItem?.Dosage ?? string.Empty,
            log.PrescriptionItem?.Instructions);
    }
}