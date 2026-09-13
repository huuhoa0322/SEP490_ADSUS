using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>Bệnh nhân tự đăng ký qua Mobile app — xác thực số điện thoại bằng Firebase Phone
/// Auth (SDK chạy trên Mobile), backend chỉ verify Firebase ID Token rồi tạo tài khoản.</summary>
public interface IPatientSelfRegistrationService
{
    /// <summary>Throw BusinessException nếu Firebase ID Token không hợp lệ/hết hạn/thiếu số
    /// điện thoại; ConflictException nếu số điện thoại đã có tài khoản.</summary>
    Task<LoginResponse> CompleteRegistrationAsync(
        CompleteRegistrationRequest request, CancellationToken cancellationToken = default);
}
