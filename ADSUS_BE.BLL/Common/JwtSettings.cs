namespace ADSUS_BE.BLL.Common;

/// <summary>
/// JWT configuration, read from User Secrets. NEVER put it in appsettings.json — that file
/// is committed.
///
/// SecretKey must be identical on every machine in the team. If each developer generates
/// their own key, a token issued on one machine is rejected on another and you get 401s
/// that are very hard to trace back to the cause.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>Signing key. At least 256 bits — .NET throws at startup if it is shorter.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Who issued the token.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Who the token is for — shared by the web and mobile clients.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Token lifetime in minutes. There is no refresh token yet, so once it expires the user
    /// has to sign in again.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;
}
