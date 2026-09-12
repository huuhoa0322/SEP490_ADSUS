using System.Security.Cryptography;
using System.Text;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// UC-03 — đường tự phục vụ thứ 3 (SMS OTP, chỉ Patient), thêm bên cạnh
/// <see cref="PasswordResetService"/> (email) đã có — xem Global Constraints trong plan gốc.
/// Hằng số/helper băm dưới đây trùng lặp có chủ đích với
/// <see cref="PatientSelfRegistrationService"/> — không tách chung (xem Global Constraints).
/// </summary>
public class PasswordResetOtpService : IPasswordResetOtpService
{
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ResetTokenValidity = TimeSpan.FromMinutes(10);
    private const int MaxWrongAttempts = 5;

    private readonly IUserRepository _users;
    private readonly IPatientRegistrationOtpRepository _otps;
    private readonly IOtpSmsService _sms;
    private readonly IAuthService _auth;
    private readonly ILogger<PasswordResetOtpService> _logger;

    public PasswordResetOtpService(
        IUserRepository users,
        IPatientRegistrationOtpRepository otps,
        IOtpSmsService sms,
        IAuthService auth,
        ILogger<PasswordResetOtpService> logger)
    {
        _users = users;
        _otps = otps;
        _sms = sms;
        _auth = auth;
        _logger = logger;
    }

    public async Task<PasswordResetOtpResult> RequestOtpAsync(
        RequestPasswordResetOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.PhoneNumber.Trim();

        var latest = await _otps.GetLatestByPhoneAsync(phone, cancellationToken);
        if (latest is not null && DateTime.UtcNow - latest.CreatedAt < MinimumResendInterval)
        {
            return PasswordResetOtpResult.TooSoon;
        }

        // NGƯỢC điều kiện của PatientSelfRegistrationService.RequestOtpAsync (Task 5): ở đây
        // số PHẢI đã có tài khoản Patient Active. Không đủ điều kiện thì coi như "không tìm
        // thấy" y hệt nhau — không disclose khác biệt giữa 3 lý do (xem Global Constraints).
        var user = await _users.GetByPhoneReadOnlyAsync(phone, cancellationToken);
        if (user is null || user.Role != UserRole.Patient || user.Status != UserStatus.Active)
        {
            _logger.LogInformation("Forgot-password OTP cho số không đủ điều kiện — báo 'không tìm thấy'.");
            return PasswordResetOtpResult.PhoneNotFound;
        }

        var otpCode = OtpCodeGenerator.Generate();
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = phone,
            OtpHash = HashValue(otpCode),
            ExpiresAt = DateTime.UtcNow.Add(OtpValidity),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        await _otps.CreateAsync(otp, cancellationToken);

        var sent = await _sms.SendOtpAsync(phone, otpCode, cancellationToken);
        if (!sent)
        {
            _logger.LogError("Gửi OTP quên mật khẩu qua SMS thất bại cho một số điện thoại.");
        }

        return PasswordResetOtpResult.Success;
    }

    public async Task<VerifyPasswordResetOtpResult> VerifyOtpAsync(
        VerifyPasswordResetOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.PhoneNumber.Trim();

        var otp = await _otps.GetLatestByPhoneAsync(phone, cancellationToken);
        if (otp is null || otp.ExpiresAt < DateTime.UtcNow || otp.AttemptCount >= MaxWrongAttempts)
        {
            return new VerifyPasswordResetOtpResult(false, null);
        }

        if (!FixedTimeEquals(otp.OtpHash, HashValue(request.OtpCode)))
        {
            otp.AttemptCount += 1;
            await _otps.SaveChangesAsync(cancellationToken);
            return new VerifyPasswordResetOtpResult(false, null);
        }

        var resetToken = GenerateSecureToken();
        otp.VerifiedAt = DateTime.UtcNow;
        otp.VerificationTokenHash = HashValue(resetToken);
        otp.VerificationTokenExpiresAt = DateTime.UtcNow.Add(ResetTokenValidity);
        await _otps.SaveChangesAsync(cancellationToken);

        return new VerifyPasswordResetOtpResult(true, resetToken);
    }

    public async Task<LoginResponse> CompleteAsync(
        CompletePasswordResetWithOtpRequest request, CancellationToken cancellationToken = default)
    {
        var otp = await _otps.GetByVerificationTokenHashAsync(
            HashValue(request.ResetToken), cancellationToken);

        if (otp is null || otp.VerifiedAt is null
            || otp.VerificationTokenExpiresAt is null
            || otp.VerificationTokenExpiresAt < DateTime.UtcNow)
        {
            throw new BusinessException("Reset token is invalid or has expired. Please verify OTP again.");
        }

        // Kiểm lại — mirror lý do ở CompleteRegistrationAsync (Task 5): tài khoản có thể vừa
        // bị đổi trạng thái/vai trò trong lúc chờ giữa verify-otp và complete.
        var lookup = await _users.GetByPhoneReadOnlyAsync(otp.Phone, cancellationToken);
        if (lookup is null || lookup.Role != UserRole.Patient || lookup.Status != UserStatus.Active)
        {
            throw new BusinessException("This account is no longer eligible for a self-service password reset.");
        }

        var user = await _users.GetForUpdateAsync(lookup.UserId, cancellationToken)
            ?? throw new BusinessException("This account is no longer eligible for a self-service password reset.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        // Tự chọn mật khẩu mới ngay lúc reset — không cần ép đổi lại (giống lý do ở
        // CompleteRegistrationAsync, Task 5).
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Patient {UserId} completed a self-service password reset via SMS OTP", user.UserId);

        var loginResponse = await _auth.LoginAsync(
            new LoginRequest { PhoneNumber = user.Phone, Password = request.NewPassword }, cancellationToken);

        return loginResponse
            ?? throw new BusinessException("Password was reset but automatic sign-in failed. Please sign in manually.");
    }

    // ---- helpers ----

    private static string HashValue(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
