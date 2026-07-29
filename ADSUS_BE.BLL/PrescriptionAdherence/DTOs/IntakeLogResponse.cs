using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Response DTO cho GET /me/medication-intakes (Patient xem lịch uống).
/// Status derive từ ConfirmedAt (master convention — không có column status ở DB).
/// </summary>
public sealed record IntakeLogResponse(
    Guid IntakeId,
    Guid PrescriptionItemId,
    DateTime ScheduledTime,
    DateTime? ConfirmedAt,
    string Status);

public static class IntakeLogResponseMapper
{
    public static IntakeLogResponse FromEntity(MedicationIntakeLog log)
        => new(
            log.IntakeId,
            log.PrescriptionItemId,
            log.ScheduledTime,
            log.ConfirmedAt,
            log.ConfirmedAt.HasValue ? "TAKEN" : "PENDING");
}