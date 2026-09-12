namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Kết quả request-otp cho quên mật khẩu — NGƯỢC điều kiện với đăng ký
/// (RegistrationOtpResult): ở đây số PHẢI đã có tài khoản Patient Active.</summary>
public enum PasswordResetOtpResult
{
    Success,
    TooSoon,

    /// <summary>Số chưa có tài khoản, hoặc có nhưng không phải Patient, hoặc đã Deactivated —
    /// cả 3 trường hợp đều trả về giá trị này, không phân biệt (xem Global Constraints).</summary>
    PhoneNotFound,
}
