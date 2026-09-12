using System.Globalization;
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
/// Bệnh nhân tự đăng ký qua Mobile app (xem Global Constraints trong plan gốc cho toàn bộ
/// quyết định thiết kế). Không đụng tới AuthService/UserAccountService/PatientAccountService.
/// </summary>
public class PatientSelfRegistrationService : IPatientSelfRegistrationService
{
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RegistrationTokenValidity = TimeSpan.FromMinutes(10);
    private const int MaxWrongAttempts = 5;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IUserRepository _users;
    private readonly IPatientRegistrationOtpRepository _otps;
    private readonly IOtpSmsService _sms;
    private readonly IAuthService _auth;
    private readonly ILogger<PatientSelfRegistrationService> _logger;

    public PatientSelfRegistrationService(
        IUserRepository users,
        IPatientRegistrationOtpRepository otps,
        IOtpSmsService sms,
        IAuthService auth,
        ILogger<PatientSelfRegistrationService> logger)
    {
        _users = users;
        _otps = otps;
        _sms = sms;
        _auth = auth;
        _logger = logger;
    }

    public async Task<RegistrationOtpResult> RequestOtpAsync(
        RequestRegistrationOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.PhoneNumber.Trim();

        // Chống spam theo SỐ ĐIỆN THOẠI — khác RateLimitPolicies.Auth (theo IP), xem Global
        // Constraints. Kiểm CreatedAt của hàng gần nhất, bất kể hàng đó có verified/expired
        // hay chưa — xin mã mới trong vòng 60 giây là chặn, dù mã cũ còn hiệu lực hay không.
        var latest = await _otps.GetLatestByPhoneAsync(phone, cancellationToken);
        if (latest is not null && DateTime.UtcNow - latest.CreatedAt < MinimumResendInterval)
        {
            return RegistrationOtpResult.TooSoon;
        }

        // Báo RÕ cho client khi số đã có tài khoản (xem Global Constraints — quyết định có
        // chủ đích, đảo ngược thiết kế anti-enumeration nháp ban đầu). Không tạo hàng OTP,
        // không gửi SMS cho trường hợp này.
        if (await _users.PhoneExistsAsync(phone, cancellationToken))
        {
            _logger.LogInformation("Request-otp cho số đã có tài khoản — không gửi SMS, báo lỗi rõ cho client.");
            return RegistrationOtpResult.PhoneAlreadyRegistered;
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
            // Không throw — hàng OTP đã lưu, người dùng có thể bấm "Gửi lại" sau 60 giây.
            // Log đủ để vận hành biết SMS đang lỗi, không log otpCode.
            _logger.LogError("Gửi OTP qua SMS thất bại cho một số điện thoại.");
        }

        return RegistrationOtpResult.Success;
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(
        VerifyRegistrationOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.PhoneNumber.Trim();

        var otp = await _otps.GetLatestByPhoneAsync(phone, cancellationToken);
        if (otp is null || otp.ExpiresAt < DateTime.UtcNow || otp.AttemptCount >= MaxWrongAttempts)
        {
            return new VerifyOtpResult(false, null);
        }

        if (!FixedTimeEquals(otp.OtpHash, HashValue(request.OtpCode)))
        {
            otp.AttemptCount += 1;
            await _otps.SaveChangesAsync(cancellationToken);
            return new VerifyOtpResult(false, null);
        }

        var registrationToken = GenerateSecureToken();
        otp.VerifiedAt = DateTime.UtcNow;
        otp.VerificationTokenHash = HashValue(registrationToken);
        otp.VerificationTokenExpiresAt = DateTime.UtcNow.Add(RegistrationTokenValidity);
        await _otps.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Xác thực OTP tự đăng ký thành công cho một số điện thoại.");

        return new VerifyOtpResult(true, registrationToken);
    }

    public async Task<LoginResponse> CompleteRegistrationAsync(
        CompleteRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var otp = await _otps.GetByVerificationTokenHashAsync(
            HashValue(request.RegistrationToken), cancellationToken);

        if (otp is null || otp.VerifiedAt is null
            || otp.VerificationTokenExpiresAt is null
            || otp.VerificationTokenExpiresAt < DateTime.UtcNow)
        {
            throw new BusinessException("Registration token is invalid or has expired. Please verify OTP again.");
        }

        var phone = otp.Phone;

        // Kiểm lại — token đã verify từ vài phút trước, số này có thể vừa bị đăng ký qua
        // đường khác (Admin/Điều dưỡng) trong lúc chờ. An toàn để lộ "đã đăng ký" ở ĐÂY, vì
        // người gọi đã chứng minh họ kiểm soát chính số điện thoại đó (khác lúc request-otp,
        // khi họ chưa chứng minh gì).
        if (await _users.PhoneExistsAsync(phone, cancellationToken))
        {
            throw new ConflictException("This phone number is already registered.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = phone,
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            // Tự chọn mật khẩu ngay lúc đăng ký — khác luồng Admin/Điều dưỡng tạo hộ (mật khẩu
            // tạm do hệ thống sinh), nên không cần ép đổi lại ở lần đăng nhập đầu.
            MustChangePassword = false,
            BiometricEnabled = false,
            DateOfBirth = ParseDateOrNull(request.DateOfBirth),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Patient {UserId} self-registered successfully", user.UserId);

        // Tái dùng LoginAsync để phát access + refresh token — không viết lại logic JWT lần 3.
        var loginResponse = await _auth.LoginAsync(
            new LoginRequest { PhoneNumber = phone, Password = request.Password }, cancellationToken);

        // Tài khoản vừa tạo Active với đúng mật khẩu vừa hash — LoginAsync không thể trả null
        // ở đây trừ khi có race condition cực hiếm (tài khoản bị vô hiệu hoá đúng lúc này).
        return loginResponse
            ?? throw new BusinessException("Account was created but automatic sign-in failed. Please sign in manually.");
    }

    // ---- helpers ----

    private static DateOnly? ParseDateOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

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
}
