using System.Globalization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bệnh nhân tự đăng ký qua Mobile app hoặc Web Frontend, xác thực số điện thoại bằng Firebase
/// Phone Auth. Tạo đồng thời User và PatientProfile trong transaction, hỗ trợ Account Linking
/// cho hồ sơ khách vãng lai và gán giới tính tùy chọn (fallback Nữ).
/// </summary>
public class PatientSelfRegistrationService : IPatientSelfRegistrationService
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPatientProfileService _patientProfiles;
    private readonly IUserRepository _users;
    private readonly IFirebasePhoneVerificationService _firebase;
    private readonly IAuthService _auth;
    private readonly ILogger<PatientSelfRegistrationService> _logger;

    public PatientSelfRegistrationService(
        IUnitOfWork unitOfWork,
        IPatientProfileService patientProfiles,
        IUserRepository users,
        IFirebasePhoneVerificationService firebase,
        IAuthService auth,
        ILogger<PatientSelfRegistrationService> logger)
    {
        _unitOfWork = unitOfWork;
        _patientProfiles = patientProfiles;
        _users = users;
        _firebase = firebase;
        _auth = auth;
        _logger = logger;
    }

    public PatientSelfRegistrationService(
        IUserRepository users,
        IFirebasePhoneVerificationService firebase,
        IAuthService auth,
        ILogger<PatientSelfRegistrationService> logger)
        : this(null!, null!, users, firebase, auth, logger)
    {
    }

    public async Task<LoginResponse> CompleteRegistrationAsync(
        CompleteRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var phone = await _firebase.VerifyAndGetLocalPhoneNumberAsync(
            request.FirebaseIdToken, cancellationToken);

        if (phone is null)
        {
            throw new BusinessException(
                "Firebase ID token is invalid or has expired. Please verify your phone number again.");
        }

        // Token đã chứng minh SỞ HỮU số điện thoại đó — an toàn để lộ "đã đăng ký" ở đây, khác
        // hẳn một request-otp công khai chưa chứng minh gì (không còn tồn tại trong thiết kế
        // này nữa, nhưng vẫn giữ nguyên lý do cũ cho việc dùng ConflictException ở đây).
        if (await _users.PhoneExistsAsync(phone, cancellationToken))
        {
            throw new ConflictException("This phone number is already registered.");
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

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
            MustChangePassword = false,
            Gender = EnumExtensions.ParseGenderType(request.Gender) ?? GenderType.Female,
            DateOfBirth = ParseDateOrNull(request.DateOfBirth),
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _users.AddAsync(user, cancellationToken);

            // Hồ sơ bệnh nhân (module MedicalRecord): nhận lại guest profile trùng SĐT — xoá các
            // trường guest — hoặc tạo hồ sơ mới. Lưu cùng lượt với tài khoản.
            await _patientProfiles.StageForNewPatientAsync(user, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation("Patient {UserId} self-registered via Firebase Phone Auth", user.UserId);

        var loginResponse = await _auth.LoginAsync(
            new LoginRequest { PhoneNumber = phone, Password = request.Password }, cancellationToken);

        return loginResponse
            ?? throw new BusinessException("Account was created but automatic sign-in failed. Please sign in manually.");
    }

    private static DateOnly? ParseDateOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
