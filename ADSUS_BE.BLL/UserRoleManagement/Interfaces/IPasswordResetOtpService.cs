using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>UC-03 — đường tự phục vụ thứ 3 bên cạnh IPasswordResetService (email), chỉ Patient,
/// xác thực số điện thoại bằng Firebase Phone Auth (đổi từ OTP tự quản lý).</summary>
public interface IPasswordResetOtpService
{
    /// <summary>Throw BusinessException CHỈ khi Firebase ID Token không hợp lệ/hết hạn/thiếu
    /// claim số điện thoại — đây là lỗi input thật sự. KHÔNG throw cho bất kỳ trường hợp số điện
    /// thoại không đủ điều kiện nào (không tìm thấy tài khoản / không phải Patient / không
    /// Active) — cả 3 trường hợp này đều TRẢ VỀ null giống hệt nhau, không phân biệt lý do ra
    /// ngoài, để giữ đúng tinh thần "báo RÕ 404" nhất quán ở tầng controller (xem Task 5).</summary>
    Task<LoginResponse?> CompleteAsync(
        CompletePasswordResetWithFirebaseRequest request, CancellationToken cancellationToken = default);
}
