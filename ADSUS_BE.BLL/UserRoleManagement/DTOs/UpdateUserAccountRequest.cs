namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// UC-04 FT-09 — Admin sửa thông tin tài khoản và phân lại vai trò trên SCR-07.
///
/// Không có số điện thoại: đó là định danh đăng nhập (BR-02), đổi được thì người dùng mất
/// luôn đường vào. Không có trạng thái: khoá/mở/vô hiệu hoá đi theo endpoint riêng, vì mỗi
/// việc có luật riêng (vô hiệu hoá là một chiều).
/// </summary>
public class UpdateUserAccountRequest
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Chỉ nhận DOCTOR, NURSE hoặc PATIENT — xem CreateUserAccountRequest.Role.</summary>
    public string Role { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Phải đủ 18 tuổi nếu nhập; bị bỏ qua khi vai trò là PATIENT (BR-01).</summary>
    public string? DateOfBirth { get; set; }
}
