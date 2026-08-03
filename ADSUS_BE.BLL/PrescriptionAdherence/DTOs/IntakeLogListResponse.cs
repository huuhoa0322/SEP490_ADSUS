namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Module 7 UC-11 — 1 dòng trong timeline liều thuốc.
/// Status derive từ ConfirmedAt (logic nằm ở Service, khớp convention master:
/// DB không có column status, derive từ ConfirmedAt).
/// </summary>
public sealed record IntakeLogListItem(
    Guid IntakeId,
    Guid PrescriptionItemId,
    string MedicineName,
    DateTime ScheduledTime,
    DateTime? ConfirmedAt,
    string Status);

/// <summary>
/// Module 7 UC-11 — payload timeline cho 1 đơn thuốc.
/// </summary>
public sealed record IntakeLogListResponse(
    Guid PrescriptionId,
    IReadOnlyList<IntakeLogListItem> Items);
