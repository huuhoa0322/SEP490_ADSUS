namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// The "data" payload returned on a successful sign-in.
/// Never contains password_hash or any other sensitive field (api_design_rules).
/// </summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Id tài khoản đang đăng nhập.
    ///
    /// Có mặt ở đây thay vì để client gọi thêm GET /users/me: giá trị này rơi thẳng vào
    /// store phía giao diện lúc đăng nhập, không tốn thêm một vòng request ở mỗi màn cần
    /// dùng. Frontend cần nó để điền sẵn ô "Bác sĩ phụ trách" khi chính Bác sĩ tạo ca khám
    /// — GB-04 cấm backend tự suy ra người phụ trách từ token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// ADMIN / DOCTOR / PATIENT. The client uses it to route the user to their own area (BR-03).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Nullable — the email column allows NULL.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// When true the client MUST send the user straight to the change-password screen and
    /// block everything else (UC-25). Set after an admin issues a temporary password, or
    /// when the account was just created.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
