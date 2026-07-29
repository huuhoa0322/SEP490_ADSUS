namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-25 — a signed-in user changes their own password. Available to every role.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmNewPassword { get; set; } = string.Empty;
}
