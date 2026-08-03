namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Module 7 UC-11 / UC-18 — chi tiết 1 đơn thuốc (dùng cho cả view + response tạo mới).
/// Items kèm adherence per-item; tổng adherencePercent/adherenceLevel ở cấp đơn.
/// </summary>
public sealed record PrescriptionDetailResponse(
    Guid PrescriptionId,
    Guid CaseId,
    Guid PatientProfileId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    DateOnly PrescribedDate,
    string Status,
    string? GeneralNote,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PrescriptionItemDetailResponse> Items,
    decimal AdherencePercent,
    string AdherenceLevel);

/// <summary>
/// 1 dòng thuốc trong đơn + thống kê adherence (đã tính).
/// </summary>
public sealed record PrescriptionItemDetailResponse(
    Guid PrescriptionItemId,
    Guid MedicineId,
    string MedicineName,
    string Dosage,
    short DurationDays,
    DateOnly StartDate,
    string? Instructions,
    int TotalDoses,
    int TakenDoses,
    int PendingDoses,
    decimal AdherencePercent,
    string AdherenceLevel);
