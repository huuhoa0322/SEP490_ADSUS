namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Kết quả request-otp — xem Global Constraints cho quyết định báo rõ khi số đã có tài khoản.</summary>
public enum RegistrationOtpResult
{
    Success,

    /// <summary>Xin mã quá gần lần trước (dưới 60 giây) — chưa tới 60s thì không tạo hàng mới,
    /// không gửi SMS.</summary>
    TooSoon,

    /// <summary>Số điện thoại đã có tài khoản Active — không tạo hàng OTP, không gửi SMS.
    /// Controller (Task 6) dịch giá trị này sang HTTP 409 kèm thông báo rõ "Số điện thoại này
    /// đã tồn tại." (quyết định có chủ đích, xem Global Constraints).</summary>
    PhoneAlreadyRegistered,
}
