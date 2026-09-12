namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Bước 1 của tự đăng ký — xin gửi mã OTP tới một số điện thoại.</summary>
public record RequestRegistrationOtpRequest(string PhoneNumber);
