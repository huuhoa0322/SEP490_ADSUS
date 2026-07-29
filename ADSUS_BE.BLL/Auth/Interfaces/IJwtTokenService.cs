using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.Auth.Interfaces;

public interface IJwtTokenService
{
    /// <summary>
    /// Issues an access token for an authenticated user.
    /// The token carries the user id, phone number, full name and role — enough for
    /// [Authorize(Roles = "...")] to work.
    /// </summary>
    string GenerateAccessToken(User user);
}
