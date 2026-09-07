using ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;

namespace ADSUS_BE.BLL.DoctorMedicationTracking.Interfaces;

public interface IDoctorMedicationTrackingService
{
    /// <param name="nowUtc">Mốc thời gian hiện tại. Dùng cho test deterministic; production truyền DateTime.UtcNow.</param>
    Task<DoctorPatientListResponse> GetPatientListAsync(
        Guid doctorId,
        string? search,
        string? adherenceLevel,
        bool? hasOverdueDoses,
        DateTime? nowUtc = null,
        CancellationToken ct = default);

    /// <param name="nowUtc">Mốc thời gian hiện tại.</param>
    Task<PatientPrescriptionDetailResponse> GetPatientDetailAsync(
        Guid doctorId,
        Guid patientId,
        DateTime? nowUtc = null,
        CancellationToken ct = default);

    /// <param name="nowUtc">Mốc thời gian hiện tại.</param>
    Task<RemindResponse> SendRemindersAsync(
        Guid doctorId,
        Guid patientId,
        RemindRequest request,
        DateTime? nowUtc = null,
        CancellationToken ct = default);
}
