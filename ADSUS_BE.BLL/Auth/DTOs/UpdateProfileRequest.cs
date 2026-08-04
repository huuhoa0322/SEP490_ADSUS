namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-10 — bệnh nhân tự cập nhật hồ sơ cá nhân của mình.
///
/// Chỉ đúng ba trường này được sửa. Số điện thoại KHÔNG có trong đây vì nó là định danh
/// đăng nhập, muốn đổi phải liên hệ phòng khám (BR-02 và AF-02).
/// Dữ liệu y tế thì tuyệt đối không sửa được từ đây (BR-03).
/// </summary>
public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Định dạng yyyy-MM-dd. Để trống nếu không muốn khai.</summary>
    public string? DateOfBirth { get; set; }
}
