namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Luật định dạng số điện thoại, khai ở ĐÚNG MỘT CHỖ.
///
/// Trước đây mỗi validator tự viết lấy: màn tạo tài khoản bắt <c>^0\d{8,10}$</c>, còn màn
/// quên mật khẩu và đăng nhập thì chỉ kiểm độ dài tối đa. Hệ quả: Admin tạo tài khoản thì bị
/// chặn số sai định dạng, nhưng cùng số đó gõ ở màn quên mật khẩu lại đi lọt tới tận
/// database. Gom về một hằng số để không thể lệch nhau nữa — sửa luật thì sửa một chỗ.
///
/// ĐÚNG 10 CHỮ SỐ, bắt đầu bằng 0 (quyết định của nhóm 04/08/2026). Số di động Việt Nam sau
/// đợt chuyển đổi năm 2018 đều là 10 chữ số; khoảng 9–11 cũ quá rộng, gõ thiếu hay thừa một
/// số vẫn lọt qua.
/// </summary>
public static class PhoneNumberRule
{
    /// <summary>Biểu thức chính quy dùng cho FluentValidation.</summary>
    public const string Pattern = @"^0\d{9}$";

    /// <summary>Số chữ số, dùng cho giới hạn độ dài.</summary>
    public const int Length = 10;

    /// <summary>Thông báo khi sai định dạng. Đã có bản dịch tiếng Việt ở api-messages.ts.</summary>
    public const string Message = "Phone number must start with 0 and contain exactly 10 digits.";
}
