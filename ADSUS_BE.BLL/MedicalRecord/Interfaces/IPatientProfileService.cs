using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface IPatientProfileService
{
    Task<PatientProfileResponse> CreateAsync(
        CreatePatientProfileRequest request, Guid actingUserId, CancellationToken ct = default);

    Task<PatientProfileResponse> UpdateAsync(
        Guid patientProfileId, UpdatePatientProfileRequest request, CancellationToken ct = default);

    Task<PatientProfileResponse> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default);

    Task<PagedResult<PatientSummaryResponse>> SearchPatientsAsync(
        string? search,
        string? visitStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
