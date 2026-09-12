namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Trả về sau khi verify-otp thành công. Client phải cầm RegistrationToken này để gọi
/// complete — không được gửi lại PhoneNumber/OtpCode ở bước đó.
/// </summary>
public record VerifyRegistrationOtpResponse(string RegistrationToken);
