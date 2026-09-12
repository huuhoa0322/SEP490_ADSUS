namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Bước 2 của tự đăng ký — xác thực mã OTP vừa nhận qua SMS.</summary>
public record VerifyRegistrationOtpRequest(string PhoneNumber, string OtpCode);
