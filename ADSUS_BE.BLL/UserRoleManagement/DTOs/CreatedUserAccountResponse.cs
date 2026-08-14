namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Trả về SAU KHI TẠO tài khoản (UC-04 FT-07) — DUY NHẤT nơi mật khẩu tạm xuất hiện dưới dạng
/// plaintext, đúng một lần tại thời điểm tạo (sửa 12/08/2026, thống nhất với UC-03 AF-02/UC-06
/// AF-01/AF-03 — cùng cách làm với PatientAccountCreatedResponse của Module 04). Admin đọc
/// trực tiếp cho chủ tài khoản nghe/ghi lại — không còn gửi qua email. UserAccountResponse
/// (Search/GetById) không có trường này dưới bất kỳ hình thức nào.
/// </summary>
public sealed class CreatedUserAccountResponse
{
    public UserAccountResponse Account { get; init; } = null!;

    public string TemporaryPassword { get; init; } = string.Empty;
}
