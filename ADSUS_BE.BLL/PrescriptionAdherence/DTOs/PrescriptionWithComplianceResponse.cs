namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Response cho GET /api/v1/cases/{caseId}/prescriptions/with-compliance.
/// Chỉ đơn do actor (bác sĩ hiện tại) kê mới có AdherencePercent;
/// đơn bác sĩ khác → null (GB guard).
/// </summary>
public sealed record PrescriptionWithComplianceResponse(
    Guid PrescriptionId,
    Guid CaseId,
    Guid DoctorId,
    DateOnly PrescribedDate,
    string? GeneralNote,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>
    /// Tỉ lệ tuân thủ của toàn đơn. Null nếu actor không phải bác sĩ kê đơn này.
    /// </summary>
    double? AdherencePercent,
    IReadOnlyList<PrescriptionItemWithComplianceResponse> Items);

public sealed record PrescriptionItemWithComplianceResponse(
    Guid PrescriptionItemId,
    Guid MedicineId,
    string MedicineName,
    string Dosage,
    short DurationDays,
    DateOnly StartDate,
    string? Instructions,
    IReadOnlyList<string>? ScheduleSlots,
    /// <summary>
    /// Tỉ lệ tuân thủ của dòng thuốc này. Null nếu actor không phải bác sĩ kê đơn.
    /// </summary>
    double? AdherencePercent);
