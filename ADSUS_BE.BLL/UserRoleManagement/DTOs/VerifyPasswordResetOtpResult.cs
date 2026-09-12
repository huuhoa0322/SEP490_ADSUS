namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

public record VerifyPasswordResetOtpResult(bool Success, string? ResetToken);
