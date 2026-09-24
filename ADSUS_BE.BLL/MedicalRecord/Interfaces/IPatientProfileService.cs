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

    /// <summary>
    /// Như <see cref="GetByIdAsync"/> nhưng trả null thay vì ném lỗi khi không có hồ sơ — cho
    /// module khác cần thông tin bệnh nhân để gửi thông báo "nếu có" (vd AppointmentService).
    /// Hồ sơ guest (người thân chưa có tài khoản) trả PatientUserId = Guid.Empty.
    /// </summary>
    Task<PatientProfileResponse?> FindByIdAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>
    /// PatientProfileId của một tài khoản bệnh nhân, null nếu tài khoản chưa có hồ sơ — cho module
    /// khác chỉ cần biết "hồ sơ của người đang đăng nhập" (lời nhắc, lịch uống thuốc...). Không tạo bù.
    /// </summary>
    Task<Guid?> FindIdByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<PagedResult<PatientSummaryResponse>> SearchPatientsAsync(
        string? search,
        string? visitStatus,
        bool? hasProfile,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
