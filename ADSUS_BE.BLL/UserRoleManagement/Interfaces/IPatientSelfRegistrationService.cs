using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>Bệnh nhân tự đăng ký qua Mobile app — xác thực số điện thoại bằng OTP trước khi
/// tạo tài khoản. Không liên quan tới <c>IAuthService.LoginAsync</c> (đăng nhập không đổi).</summary>
public interface IPatientSelfRegistrationService
{
    Task<RegistrationOtpResult> RequestOtpAsync(
        RequestRegistrationOtpRequest request, CancellationToken cancellationToken = default);

    Task<VerifyOtpResult> VerifyOtpAsync(
        VerifyRegistrationOtpRequest request, CancellationToken cancellationToken = default);

    /// <summary>Tạo tài khoản Patient thật rồi tự động đăng nhập (tái dùng IAuthService.LoginAsync).
    /// Throw BusinessException nếu registration token không hợp lệ/hết hạn; ConflictException nếu
    /// số điện thoại vừa bị người khác đăng ký trong lúc chờ (race condition hiếm).</summary>
    Task<LoginResponse> CompleteRegistrationAsync(
        CompleteRegistrationRequest request, CancellationToken cancellationToken = default);
}
