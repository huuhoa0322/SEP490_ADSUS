namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// UC-04 FT-07 — Admin tạo tài khoản đăng nhập trên SCR-07.
///
/// KHÔNG có trường mật khẩu. Theo ghi chú nhất quán trong UC-04, mật khẩu tạm luôn do hệ
/// thống sinh ra rồi gửi qua email, không bao giờ để Admin tự đặt và không bao giờ hiển thị
/// dạng đọc được cho bất kỳ ai (PRD §6.2).
/// </summary>
public class CreateUserAccountRequest
{
    /// <summary>Định danh đăng nhập, duy nhất toàn hệ thống (BR-02).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Chỉ nhận DOCTOR, NURSE hoặc PATIENT.
    ///
    /// ADMIN bị từ chối: theo UC-04, tài khoản Admin được cấp lúc dựng hệ thống chứ không
    /// tạo qua màn này. Nhận kiểu chuỗi để trả về lỗi rõ ràng thay vì để bộ chuyển đổi JSON
    /// âm thầm biến giá trị lạ thành phần tử đầu tiên của enum.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Tuỳ chọn. Chỉ dùng để cấp lại mật khẩu (UC-03), không dùng để đăng nhập.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Tuỳ chọn, định dạng yyyy-MM-dd. Người được tạo tài khoản phải đủ 18 tuổi.
    ///
    /// BR-01: với vai trò PATIENT thì trường này bị BỎ QUA hoàn toàn ở tầng nghiệp vụ, kể cả
    /// khi người gọi có gửi lên. Ngày sinh của bệnh nhân là dữ liệu y tế, Admin không được
    /// chạm tới — giao diện ẩn đi là chưa đủ, vì ai cũng gọi thẳng API được.
    /// </summary>
    public string? DateOfBirth { get; set; }
}
