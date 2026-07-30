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

    /// <summary>
    /// Cờ đăng nhập sinh trắc học (UC-02). Ứng dụng di động dùng để biết có hiện nút
    /// đăng nhập bằng vân tay hay không.
    /// </summary>
    public bool BiometricEnabled { get; set; }

    /// <summary>
    /// UC-25 — tài khoản đang bị buộc đổi mật khẩu.
    ///
    /// Có mặt ở đây vì đăng nhập bằng vân tay KHÔNG đi qua /auth/login, nên không nhận được
    /// cờ này từ LoginResponse. Thiếu nó thì kịch bản sau lọt lưới: Admin cấp lại mật khẩu
    /// cho một tài khoản đã bật sẵn vân tay, người dùng quét vân tay và vào thẳng ứng dụng,
    /// bỏ qua hoàn toàn màn ép đổi mật khẩu.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
