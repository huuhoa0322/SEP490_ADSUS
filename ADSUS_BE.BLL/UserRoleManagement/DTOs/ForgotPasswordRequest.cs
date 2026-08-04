namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// UC-03 FT-06 — người dùng tự yêu cầu cấp lại mật khẩu từ màn đăng nhập.
///
/// BR-01: phải khớp CẢ số điện thoại LẪN email của một tài khoản đang tồn tại. Chỉ hỏi số
/// điện thoại thì ai biết số của người khác cũng đặt lại được mật khẩu của họ.
/// </summary>
public class ForgotPasswordRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
