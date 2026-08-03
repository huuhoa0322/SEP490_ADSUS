using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface IPatientProfileService
{
    Task<PatientProfileResponse> CreateAsync(
        CreatePatientProfileRequest request, Guid actingUserId, CancellationToken ct = default);

    Task<PatientProfileResponse> UpdateAsync(
        Guid patientProfileId, UpdatePatientProfileRequest request, CancellationToken ct = default);

    Task<PatientProfileResponse> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default);
}
