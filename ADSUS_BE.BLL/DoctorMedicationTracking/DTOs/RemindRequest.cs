namespace ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;

public sealed record RemindRequest(Guid PrescriptionId);

public sealed record RemindResponse(int SentCount, string Message);
