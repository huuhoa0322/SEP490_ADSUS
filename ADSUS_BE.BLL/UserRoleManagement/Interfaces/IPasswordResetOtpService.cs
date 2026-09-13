using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>UC-03 — đường tự phục vụ thứ 3 bên cạnh IPasswordResetService (email), chỉ Patient,
/// xác thực số điện thoại bằng Firebase Phone Auth (đổi từ OTP tự quản lý).</summary>
public interface IPasswordResetOtpService
{
    /// <summary>Throw BusinessException nếu Firebase ID Token không hợp lệ/hết hạn, hoặc tài
    /// khoản không phải Patient Active; không throw nếu số chưa có tài khoản — TRẢ VỀ null để
    /// giữ đúng tinh thần "báo RÕ 404" ở tầng controller (xem Task 5).</summary>
    Task<LoginResponse?> CompleteAsync(
        CompletePasswordResetWithFirebaseRequest request, CancellationToken cancellationToken = default);
}
