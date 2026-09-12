using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>UC-03 — thêm một đường tự phục vụ THỨ BA bên cạnh <c>IPasswordResetService</c>
/// (email) đã có: chỉ cho Patient, qua SMS OTP. Không thay, không đụng 2 đường cũ.</summary>
public interface IPasswordResetOtpService
{
    Task<PasswordResetOtpResult> RequestOtpAsync(
        RequestPasswordResetOtpRequest request, CancellationToken cancellationToken = default);

    Task<VerifyPasswordResetOtpResult> VerifyOtpAsync(
        VerifyPasswordResetOtpRequest request, CancellationToken cancellationToken = default);

    /// <summary>Throw BusinessException nếu reset token không hợp lệ/hết hạn, hoặc tài khoản
    /// không còn đủ điều kiện (đổi vai trò/Deactivated giữa lúc chờ).</summary>
    Task<LoginResponse> CompleteAsync(
        CompletePasswordResetWithOtpRequest request, CancellationToken cancellationToken = default);
}
