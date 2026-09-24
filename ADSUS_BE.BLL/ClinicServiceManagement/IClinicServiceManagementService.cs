using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;

namespace ADSUS_BE.BLL.ClinicServiceManagement;

public interface IClinicServiceManagementService
{
    Task<IReadOnlyList<ClinicServiceResponse>> GetAllAsync(bool? isActive, CancellationToken ct = default);
    Task<ClinicServiceResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Như <see cref="GetByIdAsync"/> nhưng trả null thay vì ném lỗi — cho module khác tra danh mục.</summary>
    Task<ClinicServiceResponse?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Dịch vụ đang hoạt động có mã cho trước (vd "GENERAL_EXAM"), null nếu không có hoặc đã tắt.</summary>
    Task<ClinicServiceResponse?> FindActiveByCodeAsync(string code, CancellationToken ct = default);
    Task<ClinicServiceResponse> CreateAsync(CreateClinicServiceRequest request, CancellationToken ct = default);
    Task<ClinicServiceResponse> UpdateAsync(Guid id, UpdateClinicServiceRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}
