namespace ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;

public sealed record DoctorPatientDto(
    Guid PatientProfileId,
    string PatientName,
    int TodayTaken,
    int TodayTotal,
    decimal TodayAdherencePercent,
    string AdherenceLevel,
    bool HasOverdueToday,
    int ActivePrescriptionCount);

public sealed record DoctorPatientListResponse(
    IReadOnlyList<DoctorPatientDto> Patients,
    int TotalCount);
