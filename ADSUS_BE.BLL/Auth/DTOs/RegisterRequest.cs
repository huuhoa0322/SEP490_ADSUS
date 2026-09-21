namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-02 — yêu cầu đăng ký tài khoản bệnh nhân (Mobile).
/// Hỗ trợ Account Linking: nếu cung cấp GuestPatientProfileId, hệ thống sẽ
/// liên kết tài khoản mới với PatientProfile đã tồn tại (guest patient).
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// Số điện thoại — định danh đăng nhập duy nhất của hệ thống (BR-02).
    /// </summary>
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// Mật khẩu đăng nhập.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "Mật khẩu phải từ 8 ký tự trở lên.")]
    [System.ComponentModel.DataAnnotations.MaxLength(72)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ in hoa và 1 chữ số.")]
    public required string Password { get; init; }

    /// <summary>
    /// Xác nhận mật khẩu (phải trùng với Password).
    /// </summary>
    public required string ConfirmPassword { get; init; }

    /// <summary>
    /// Họ tên đầy đủ của bệnh nhân.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// Email (tùy chọn).
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Ngày sinh (tùy chọn, format: yyyy-MM-dd).
    /// </summary>
    public string? DateOfBirth { get; init; }

    /// <summary>
    /// Guest PatientProfile ID để Account Linking (tùy chọn).
    /// Nếu cung cấp, hệ thống sẽ liên kết tài khoản mới với PatientProfile đã tồn tại
    /// và xóa các trường guest (full_name, phone, date_of_birth).
    /// </summary>
    public Guid? GuestPatientProfileId { get; init; }
}

/// <summary>
/// Kết quả đăng ký tài khoản.
/// </summary>
public enum RegisterResult
{
    /// <summary>Đăng ký thành công.</summary>
    Success,

    /// <summary>Số điện thoại đã được sử dụng.</summary>
    PhoneAlreadyUsed,

    /// <summary>Email đã được sử dụng bởi tài khoản khác.</summary>
    EmailAlreadyUsed,

    /// <summary>Mật khẩu không khớp.</summary>
    PasswordMismatch,

    /// <summary>Guest PatientProfile không tồn tại.</summary>
    GuestProfileNotFound,

    /// <summary>Tài khoản không hợp lệ.</summary>
    InvalidAccount,
}

/// <summary>
/// Response trả về sau khi đăng ký thành công.
/// </summary>
public sealed class RegisterResponse
{
    public required Guid UserId { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
}
