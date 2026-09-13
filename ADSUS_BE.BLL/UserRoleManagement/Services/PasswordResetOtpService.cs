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
/// UC-03 — đường tự phục vụ thứ 3 (Firebase Phone Auth, chỉ Patient), thêm bên cạnh
/// <see cref="PasswordResetService"/> (email) đã có — KHÔNG đụng file đó (xem Global Constraints).
/// Đổi từ cơ chế OTP tự quản lý sang Firebase, giữ nguyên mọi luật nghiệp vụ khác.
/// </summary>
public class PasswordResetOtpService : IPasswordResetOtpService
{
    private readonly IUserRepository _users;
    private readonly IFirebasePhoneVerificationService _firebase;
    private readonly IAuthService _auth;
    private readonly ILogger<PasswordResetOtpService> _logger;

    public PasswordResetOtpService(
        IUserRepository users,
        IFirebasePhoneVerificationService firebase,
        IAuthService auth,
        ILogger<PasswordResetOtpService> logger)
    {
        _users = users;
        _firebase = firebase;
        _auth = auth;
        _logger = logger;
    }

    public async Task<LoginResponse?> CompleteAsync(
        CompletePasswordResetWithFirebaseRequest request, CancellationToken cancellationToken = default)
    {
        var phone = await _firebase.VerifyAndGetLocalPhoneNumberAsync(
            request.FirebaseIdToken, cancellationToken);

        if (phone is null)
        {
            throw new BusinessException(
                "Firebase ID token is invalid or has expired. Please verify your phone number again.");
        }

        // NGƯỢC điều kiện của PatientSelfRegistrationService: ở đây số PHẢI đã có tài khoản
        // Patient Active. Không đủ điều kiện thì trả null — controller (Task 5) dịch sang 404,
        // KHÔNG phân biệt 3 lý do (không tồn tại / vai trò khác / Deactivated) ra ngoài.
        var lookup = await _users.GetByPhoneReadOnlyAsync(phone, cancellationToken);
        if (lookup is null || lookup.Role != UserRole.Patient || lookup.Status != UserStatus.Active)
        {
            _logger.LogInformation("Forgot-password qua Firebase cho số không đủ điều kiện.");
            return null;
        }

        var user = await _users.GetForUpdateAsync(lookup.UserId, cancellationToken)
            ?? throw new BusinessException("This account is no longer eligible for a self-service password reset.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Patient {UserId} completed a self-service password reset via Firebase Phone Auth", user.UserId);

        var loginResponse = await _auth.LoginAsync(
            new LoginRequest { PhoneNumber = user.Phone, Password = request.NewPassword }, cancellationToken);

        return loginResponse
            ?? throw new BusinessException("Password was reset but automatic sign-in failed. Please sign in manually.");
    }
}
