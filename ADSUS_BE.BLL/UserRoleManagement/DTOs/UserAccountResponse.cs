namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Một dòng tài khoản trên SCR-06, và cũng là dữ liệu đổ vào form sửa ở SCR-07.
/// </summary>
public class UserAccountResponse
{
    public Guid UserId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// BR-01 — LUÔN null khi vai trò là PATIENT.
    ///
    /// Ngày sinh nằm chung bảng users nên không tách quyền theo cột được; tầng nghiệp vụ
    /// phải tự lọc. Ẩn ở giao diện là chưa đủ, vì Admin gọi thẳng API vẫn đọc được.
    /// </summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Đang bị buộc đổi mật khẩu ở lần đăng nhập tới (UC-25).</summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }
}
