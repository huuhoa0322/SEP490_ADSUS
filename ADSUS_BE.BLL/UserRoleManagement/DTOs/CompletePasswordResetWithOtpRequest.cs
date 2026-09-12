namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

public record CompletePasswordResetWithOtpRequest(
    string ResetToken,
    string NewPassword,
    string ConfirmNewPassword);
