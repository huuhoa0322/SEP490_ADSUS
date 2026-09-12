namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// Gửi mã OTP qua SMS cho luồng bệnh nhân tự đăng ký. Rập khuôn <c>IEmailService</c>: hợp
/// đồng KHÔNG BAO GIỜ throw — mọi lỗi mạng/nhà cung cấp trả về false, để tầng gọi tự quyết
/// cách phản hồi cho client.
/// </summary>
public interface IOtpSmsService
{
    Task<bool> SendOtpAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default);
}
