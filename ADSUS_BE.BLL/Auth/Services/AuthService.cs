using System.Security.Cryptography;
using System.Text;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Mappers;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ADSUS_BE.BLL.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtTokenService _tokens;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthService> _logger;
    private readonly IFcmTokenService _fcmTokenService;
    private readonly IMemoryCache? _cache;

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
        AppDbContext db,
        IFcmTokenService fcmTokenService,
        IMemoryCache? cache,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _db = db;
        _fcmTokenService = fcmTokenService;
        _cache = cache;
        _logger = logger;
    }

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IJwtTokenService tokens,
        AppDbContext db,
        ILogger<AuthService> logger)
        : this(users, refreshTokens, tokens, db, null!, null, logger)
    {
    }

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IJwtTokenService tokens,
        ILogger<AuthService> logger)
        : this(users, refreshTokens, tokens, null!, null!, null, logger)
    {
    }


    private static string ComputeSha256Hash(string rawData)
    {
        if (string.IsNullOrEmpty(rawData)) return string.Empty;
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToBase64String(bytes);
    }

    private static string SanitizeForLog(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace(Environment.NewLine, "_").Replace("\n", "_").Replace("\r", "_");
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var safePhoneLog = SanitizeForLog(request.PhoneNumber);
        var cacheKey = $"FailedLogin_{ComputeSha256Hash(request.PhoneNumber)}";
        int failedAttempts = 0;

        if (_cache != null)
        {
            _cache.TryGetValue(cacheKey, out failedAttempts);
            if (failedAttempts >= 5)
            {
                _logger.LogWarning("User {Phone} is temporarily locked out due to multiple failed login attempts.", safePhoneLog);
                // Bỏ qua rule GB-06 theo yêu cầu thực tế của sếp: Hiển thị rõ thông báo khóa cho người dùng biết
                throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa tạm thời 15 phút do nhập sai mật khẩu quá 5 lần.");
            }
        }

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
            if (_cache != null)
            {
                // Increment failed attempts (even if user is null, to prevent probing phone numbers)
                failedAttempts++;
                _cache.Set(cacheKey, failedAttempts, TimeSpan.FromMinutes(15));
                if (failedAttempts >= 5)
                {
                    _logger.LogWarning("User {Phone} exceeded 5 failed login attempts. Locked out for 15 minutes.", safePhoneLog);
                    throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa tạm thời 15 phút do nhập sai mật khẩu quá 5 lần.");
                }
            }
            return null;
        }

        // Successful login: clear failed attempts
        if (_cache != null)
        {
            _cache.Remove(cacheKey);
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
        }, cancellationToken);

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
        if (_fcmTokenService != null)
        {
            await _fcmTokenService.UnregisterAllTokensAsync(userId, cancellationToken);
        }
        _logger.LogInformation("All refresh tokens and FCM tokens revoked for user {UserId}", userId);
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

    /// <summary>
    /// UC-02 — đăng ký tài khoản bệnh nhân (Mobile).
    /// Hỗ trợ Account Linking: nếu cung cấp GuestPatientProfileId, hệ thống sẽ
    /// liên kết tài khoản mới với PatientProfile đã tồn tại và xóa các trường guest.
    /// </summary>
    public async Task<(RegisterResult Result, RegisterResponse? Response)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate password confirmation
        if (request.Password != request.ConfirmPassword)
        {
            return (RegisterResult.PasswordMismatch, null);
        }

        // Check if phone already exists
        if (await _users.PhoneExistsAsync(request.PhoneNumber.Trim(), cancellationToken))
        {
            return (RegisterResult.PhoneAlreadyUsed, null);
        }

        // Check email if provided
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email != null && await _users.IsEmailUsedAsync(email, cancellationToken))
        {
            return (RegisterResult.EmailAlreadyUsed, null);
        }

        var now = DateTime.UtcNow;
        Guid userId;
        Guid patientProfileId;

        // Account Linking: tự động liên kết guest profile chưa có user_id nếu SĐT trùng khớp.
        // Dùng pessimistic locking (FOR UPDATE) để bảo đảm an toàn dữ liệu và tránh race condition.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var trimmedPhone = request.PhoneNumber.Trim();
            PatientProfile? guestProfile = null;
            if (_db.Database.IsRelational())
            {
                guestProfile = await _db.PatientProfiles
                    .FromSqlRaw(
                        "SELECT * FROM patient_profiles WHERE phone = {0} AND user_id IS NULL FOR UPDATE",
                        trimmedPhone)
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                guestProfile = await _db.PatientProfiles
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Phone == trimmedPhone && p.UserId == null, cancellationToken);
            }

            if (request.GuestPatientProfileId.HasValue && guestProfile == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (RegisterResult.GuestProfileNotFound, null);
            }

            // Check if profile already has a user
            if (guestProfile != null && guestProfile.UserId.HasValue && guestProfile.UserId.Value != Guid.Empty)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (RegisterResult.InvalidAccount, null);
            }

            // Create new user
            var newUserId = Guid.NewGuid();
            var user = new User
            {
                UserId = newUserId,
                Phone = trimmedPhone,
                FullName = request.FullName.Trim(),
                Email = email,
                Role = UserRole.Patient,
                Status = UserStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                MustChangePassword = false,
                DateOfBirth = ParseDateOrNull(request.DateOfBirth),
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Users.Add(user);

            if (guestProfile != null)
            {
                // Link user to patient profile and clear guest fields
                guestProfile.UserId = newUserId;
                guestProfile.FullName = null;
                guestProfile.Phone = null;
                guestProfile.DateOfBirth = null;
                guestProfile.UpdatedAt = now;

                patientProfileId = guestProfile.PatientProfileId;
            }
            else
            {
                // Create new patient profile
                var profile = new PatientProfile
                {
                    PatientProfileId = Guid.NewGuid(),
                    UserId = newUserId,
                    CreatedBy = newUserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                _db.PatientProfiles.Add(profile);
                patientProfileId = profile.PatientProfileId;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            userId = newUserId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Get user for token generation
        var createdUser = await _users.GetByIdReadOnlyAsync(userId, cancellationToken);
        if (createdUser == null)
        {
            return (RegisterResult.InvalidAccount, null);
        }

        // Generate tokens
        var accessToken = _tokens.GenerateAccessToken(createdUser);
        var refreshToken = GenerateSecureToken();
        await _refreshTokens.CreateAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = now,
            DeviceInfo = null
        }, cancellationToken);

        _logger.LogInformation("User {UserId} registered successfully with role {Role}", userId, createdUser.Role);

        return (RegisterResult.Success, new RegisterResponse
        {
            UserId = userId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        });
    }

    private static DateOnly? ParseDateOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParse(value, out var date)) return date;
        return null;
    }
}
