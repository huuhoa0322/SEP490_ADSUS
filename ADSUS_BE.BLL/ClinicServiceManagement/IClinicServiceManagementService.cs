using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;

namespace ADSUS_BE.BLL.ClinicServiceManagement;

public interface IClinicServiceManagementService
{
    Task<IReadOnlyList<ClinicServiceResponse>> GetAllAsync(bool? isActive, CancellationToken ct = default);
    Task<ClinicServiceResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClinicServiceResponse> CreateAsync(CreateClinicServiceRequest request, CancellationToken ct = default);
    Task<ClinicServiceResponse> UpdateAsync(Guid id, UpdateClinicServiceRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}
