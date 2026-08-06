namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-25 — a signed-in user changes their own password. Available to every role.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// Required UNLESS the account is still on an admin/nurse-issued temporary password
    /// (<c>User.MustChangePassword</c>) — the caller already proved they know it by logging
    /// in with it moments ago (extended 06/08/2026, same reveal-on-issue reasoning as UC-06
    /// AF-01/AF-03). Nullable/optional so that forced-change clients can omit it entirely;
    /// <see cref="ADSUS_BE.BLL.Auth.Services.AuthService.ChangePasswordAsync"/> enforces the
    /// conditional requirement server-side, never trusting a client-supplied flag for it.
    /// </summary>
    public string? CurrentPassword { get; set; }

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmNewPassword { get; set; } = string.Empty;
}
