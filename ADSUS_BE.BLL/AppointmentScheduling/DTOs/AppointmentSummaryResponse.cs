namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

public sealed record AppointmentSummaryResponse(
    Guid AppointmentId,
    Guid PatientProfileId,
    string PatientName,
    string Status,
    string? Reason,
    string? CancelledReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);