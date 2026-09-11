using ADSUS_BE.BLL.CaseClinicServices.DTOs;

namespace ADSUS_BE.BLL.CaseClinicServices;

public interface ICaseClinicServiceService
{
    Task<IReadOnlyList<CaseClinicServiceResponse>> GetServicesForCaseAsync(Guid caseId, CancellationToken ct = default);
    Task<CaseClinicServiceResponse> AddServiceToCaseAsync(Guid caseId, Guid clinicServiceId, CancellationToken ct = default);
    Task AddServiceToCaseByCodeAsync(Guid caseId, string serviceCode, CancellationToken ct = default);
    Task RemoveServiceFromCaseAsync(Guid caseId, Guid caseClinicServiceId, CancellationToken ct = default);
}
