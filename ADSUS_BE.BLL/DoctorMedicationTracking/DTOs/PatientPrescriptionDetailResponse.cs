namespace ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;

public sealed record TodayDoseDto(
    Guid IntakeId,
    string MedicineName,
    string Dosage,
    string ScheduledTime,
    string Status);

public sealed record AdherenceDto(
    int Taken,
    int Total,
    decimal Percent);

public sealed record PrescriptionCardDto(
    Guid PrescriptionId,
    Guid CaseId,
    string CaseName,
    IReadOnlyList<TodayDoseDto> TodayDoses,
    AdherenceDto AdherenceToday,
    AdherenceDto AdherenceOverall);

public sealed record PatientPrescriptionDetailResponse(
    string PatientName,
    IReadOnlyList<PrescriptionCardDto> Prescriptions);
