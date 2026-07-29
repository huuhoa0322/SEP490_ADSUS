namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-01 — sign in with a PHONE NUMBER, not a username or an email address.
/// Email exists only for self-service password recovery.
/// </summary>
public class LoginRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
