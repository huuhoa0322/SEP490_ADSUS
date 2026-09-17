namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-10 bước 2 — hồ sơ cá nhân hiển thị trên SCR-03.
///
/// Số điện thoại có trong response để màn hình hiển thị được, nhưng người dùng KHÔNG sửa
/// được (BR-02): đó là định danh đăng nhập duy nhất của tài khoản.
///
/// PRD không định nghĩa trường Địa chỉ hay Liên hệ khẩn cấp ở bất kỳ đâu, nên cố ý không
/// thêm vào. Muốn có thì phải đề xuất vào PRD trước.
/// </summary>
public class UserProfileResponse
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Chỉ đọc — xem BR-02.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Định dạng yyyy-MM-dd, hoặc null nếu chưa khai.</summary>
    public string? DateOfBirth { get; set; }

    public string Role { get; set; } = string.Empty;

    /// <summary>UC-25 — tài khoản đang bị buộc đổi mật khẩu.</summary>
    public bool MustChangePassword { get; set; }
}
