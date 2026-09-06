using System.Security.Cryptography;
using System.Text;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Mappers;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtTokenService _tokens;
    private readonly ILogger<AuthService> _logger;

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

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IJwtTokenService tokens,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Chỉ đọc để so mật khẩu và phát token, không sửa/lưu gì ở đây — dùng bản AsNoTracking
        // (P11 review Module 1, 14/08/2026).
        var user = await _users.GetByPhoneReadOnlyAsync(request.PhoneNumber, cancellationToken);

        var passwordMatches = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user?.PasswordHash ?? DummyHash);

        // BR-01: sign-in succeeds only when the phone number exists, the password is correct
        // AND the status is Active. A Deactivated account is rejected even with the right
        // password.
        if (user is null || !passwordMatches || user.Status != UserStatus.Active)
        {
            // LỆCH TÀI LIỆU — BR-04 chưa làm.
            //
            // UCS UC-01 BR-04 ghi: sai liên tiếp N lần thì hệ thống tự chuyển tài khoản sang
            // Locked, và có hẳn kịch bản kiểm thử cho luật này. Nhóm đã quyết bỏ vì hệ thống
            // nhỏ. Hệ quả: hiện KHÔNG có gì chặn dò mật khẩu, gọi bao nhiêu lần cũng được.
            //
            // Hướng đang bàn (chờ học chốt): sai 5 lần thì khoá 15 phút. Nếu làm thì đừng
            // đụng vào cột status — "Admin khoá" và "hệ thống tự khoá tạm" là hai việc khác
            // nhau, chính UCS cũng ghi là distinct. Thêm hai cột riêng: failed_login_count
            // và locked_until.
            //
            // Và dù có khoá tạm thì thông báo trả về vẫn phải giữ nguyên một câu duy nhất
            // (GB-06) — báo "tài khoản bị khoá 15 phút" là lộ ngay số điện thoại đó có thật.
            return null;
        }

        _logger.LogInformation(
            "User {UserId} signed in successfully with role {Role}", user.UserId, user.Role);

        var accessToken = _tokens.GenerateAccessToken(user);

        // Generate refresh token on login
        var refreshToken = GenerateSecureToken();
        await _refreshTokens.CreateAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            DeviceInfo = null
        });

        return UserMapper.ToLoginResponse(user, accessToken, refreshToken);
    }

    public async Task<RefreshTokenResponse?> RefreshTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[Auth] 🔄 Token refresh requested");

        // 1. Hash the incoming refresh token
        var tokenHash = HashToken(refreshToken);

        // 2. Get stored refresh token from DB
        var storedToken = await _refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        // 3. Validate: must exist, not revoked, not expired
        if (storedToken == null || storedToken.RevokedAt != null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            Console.WriteLine("[Auth] ❌ Token refresh FAILED - Invalid/revoked/expired token");
            _logger.LogWarning("Invalid refresh token attempted");
            return null;
        }

        // 4. Get user
        var user = await _users.GetByIdReadOnlyAsync(storedToken.UserId, cancellationToken);
        if (user == null || user.Status != UserStatus.Active)
        {
            Console.WriteLine($"[Auth] ❌ Token refresh FAILED - User inactive or deleted (UserId: {storedToken.UserId}, Status: {user?.Status})");
            _logger.LogWarning("Refresh token for inactive/deleted user: {UserId}", storedToken.UserId);
            return null;
        }

        // 5. Revoke old token (rotation)
        await _refreshTokens.RevokeAsync(storedToken.Id, cancellationToken);
        Console.WriteLine($"[Auth] 🔒 Revoked old refresh token for user {user.UserId}");

        // 6. Generate new tokens
        var newAccessToken = _tokens.GenerateAccessToken(user);
        var newRefreshToken = GenerateSecureToken();

        // 7. Save new refresh token
        await _refreshTokens.CreateAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(newRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            DeviceInfo = storedToken.DeviceInfo
        }, cancellationToken);

        Console.WriteLine($"[Auth] ✅ Token refresh SUCCESS for user {user.UserId}");
        _logger.LogInformation("Tokens refreshed for user {UserId}", user.UserId);

        return new RefreshTokenResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15)
        );
    }

    public async Task RevokeAllRefreshTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        // GetForUpdateAsync (có tracking) — hàm này sửa PasswordHash rồi SaveChangesAsync, khác
        // các nơi chỉ đọc để hiển thị (P11 review Module 1, 12/08/2026).
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);

        if (user is null)
        {
            return ChangePasswordResult.UserNotFound;
        }

        // The token may still be valid while an admin has locked the account in the meantime.
        if (user.Status != UserStatus.Active)
        {
            return ChangePasswordResult.AccountNotActive;
        }

        // BR-01: current password must match — UNLESS the account is still on a temp password
        // (MustChangePassword), where the UI omits the field and this check is skipped entirely.
        // The real barrier here is the access token, not CurrentPassword: skipping this check
        // adds no risk beyond normal bearer-token auth, but adds no second factor either —
        // anyone holding a valid token while MustChangePassword is set can change the password
        // without proving anything. Accepted trade-off for less friction, confirmed 12/08/2026 —
        // the ONLY exception to BR-01, see UC-25 BR-01/AF-03 (`Report_3.1_UCS_ADSUS.md` v1.24).
        if (!user.MustChangePassword
            && (string.IsNullOrEmpty(request.CurrentPassword)
                || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash)))
        {
            return ChangePasswordResult.CurrentPasswordIncorrect;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Clearing the flag here is what closes the admin-issued temporary password loop
        // (UC-03 / UC-04).
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} changed their password successfully", user.UserId);

        return ChangePasswordResult.Success;
    }
}
