using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Response DTO cho GET /api/v1/prescriptions/{id} và các endpoint khác.
/// Items included để caller (Doctor / Nurse / Patient) xem đầy đủ thông tin đơn.
/// AdherencePercent chưa tính ở đây — controller gọi AdherenceCalculator sau khi
/// fetch intake logs.
/// </summary>
public sealed record PrescriptionResponse(
    Guid PrescriptionId,
    Guid CaseId,
    Guid DoctorId,
    DateOnly PrescribedDate,
    string? GeneralNote,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PrescriptionItemResponse> Items);

public sealed record PrescriptionItemResponse(
    Guid PrescriptionItemId,
    Guid MedicineId,
    string MedicineName,
    string Dosage,
    short DurationDays,
    DateOnly StartDate,
    string? Instructions);

/// <summary>Map entity → response DTO.</summary>
public static class PrescriptionResponseMapper
{
    public static PrescriptionResponse FromEntity(Prescription p)
        => new(
            p.PrescriptionId,
            p.CaseId,
            p.DoctorId,
            p.PrescribedDate,
            p.GeneralNote,
            p.CreatedAt,
            p.UpdatedAt,
            p.PrescriptionItems.Select(FromEntity).ToList());

    public static PrescriptionItemResponse FromEntity(PrescriptionItem pi)
        => new(
            pi.PrescriptionItemId,
            pi.MedicineId,
            pi.Medicine?.Name ?? string.Empty,
            pi.Dosage,
            pi.DurationDays,
            pi.StartDate,
            pi.Instructions);
}