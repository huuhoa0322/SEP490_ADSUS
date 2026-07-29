using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;

    /// <summary>
    /// Dummy hash compared against when no account matches the phone number.
    ///
    /// BCrypt.Verify is intentionally slow (~100ms). Skipping it for an unknown phone number
    /// would make that response come back noticeably faster, letting an attacker enumerate
    /// which numbers are registered by timing alone — even though the error message is
    /// identical. Always running Verify keeps the timing flat. The underlying password is a
    /// random GUID generated at startup, so nobody can match it.
    /// </summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    public AuthService(IUserRepository users, IJwtTokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByPhoneAsync(request.PhoneNumber, cancellationToken);

        var passwordMatches = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user?.PasswordHash ?? DummyHash);

        // BR-01: sign-in succeeds only when the phone number exists, the password is correct
        // AND the status is Active. Locked and Deactivated are rejected even with the right
        // password.
        if (user is null || !passwordMatches || user.Status != UserStatus.Active)
        {
            return null;
        }

        return new LoginResponse
        {
            AccessToken = _tokens.GenerateAccessToken(user),
            Role = user.Role.ToApiString(),
            FullName = user.FullName,
            Email = user.Email,
            MustChangePassword = user.MustChangePassword,
        };
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return ChangePasswordResult.UserNotFound;
        }

        // The token may still be valid while an admin has locked the account in the meantime.
        if (user.Status != UserStatus.Active)
        {
            return ChangePasswordResult.AccountNotActive;
        }

        // BR-01: the change is only allowed when the current password is correct.
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ChangePasswordResult.CurrentPasswordIncorrect;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Clearing the flag here is what closes the admin-issued temporary password loop
        // (UC-03 / UC-04).
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        return ChangePasswordResult.Success;
    }
}
