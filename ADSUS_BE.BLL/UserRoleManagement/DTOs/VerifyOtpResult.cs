namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

public record VerifyOtpResult(bool Success, string? RegistrationToken);
