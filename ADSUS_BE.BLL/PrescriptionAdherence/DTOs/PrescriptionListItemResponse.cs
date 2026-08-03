namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Module 7 UC-11 — 1 dòng trong danh sách đơn thuốc của bệnh nhân.
/// AdherencePercent đã tính sẵn (gọi AdherenceCalculator ở service).
/// </summary>
public sealed record PrescriptionListItemResponse(
    Guid PrescriptionId,
    Guid CaseId,
    Guid DoctorId,
    string DoctorName,
    DateOnly PrescribedDate,
    string Status,
    int ItemCount,
    decimal AdherencePercent,
    string AdherenceLevel,
    DateTime CreatedAt);

/// <summary>
/// Module 7 UC-11 — payload list đầy đủ với thông tin phân trang.
/// </summary>
public sealed record PrescriptionListResponse(
    IReadOnlyList<PrescriptionListItemResponse> Items,
    int Total,
    int Page,
    int PageSize);
