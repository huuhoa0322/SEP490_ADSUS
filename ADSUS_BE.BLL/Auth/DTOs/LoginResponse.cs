namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// The "data" payload returned on a successful sign-in.
/// Never contains password_hash or any other sensitive field (api_design_rules).
/// </summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// ADMIN / DOCTOR / PATIENT. The client uses it to route the user to their own area (BR-03).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Nullable — the email column allows NULL.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// When true the client MUST send the user straight to the change-password screen and
    /// block everything else (UC-25). Set after an admin issues a temporary password, or
    /// when the account was just created.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
