namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

public record VerifyPasswordResetOtpRequest(string PhoneNumber, string OtpCode);
