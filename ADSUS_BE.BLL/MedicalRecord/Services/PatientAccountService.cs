using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

public sealed class PatientAccountService : IPatientAccountService
{
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IPasswordResetService _passwordReset;
    private readonly ILogger<PatientAccountService> _logger;

    public PatientAccountService(
        IUserRepository users,
        IAuditLogRepository audit,
        IPasswordResetService passwordReset,
        ILogger<PatientAccountService> logger)
    {
        _users = users;
        _audit = audit;
        _passwordReset = passwordReset;
        _logger = logger;
    }

    public async Task<PatientAccountCreatedResponse> CreateAsync(
        CreatePatientAccountRequest request, Guid actingNurseId, CancellationToken ct = default)
    {
        var phone = request.PhoneNumber.Trim();

        // UC-04 BR-02 không đổi — số điện thoại là định danh đăng nhập duy nhất toàn hệ thống.
        if (await _users.PhoneExistsAsync(phone, ct))
        {
            throw new ConflictException("This phone number is already registered.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && await _users.IsEmailUsedAsync(email, ct))
        {
            throw new ConflictException("This email is already registered.");
        }

        // BR-03 của UC-04 — mật khẩu tạm do hệ thống sinh, lưu dạng băm, buộc đổi lần đầu.
        // Trả plaintext MỘT LẦN trong response tạo tài khoản (quyết định ghi đè 06/08/2026),
        // KHÔNG lưu lại plaintext ở đâu khác, KHÔNG gửi qua email.
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = phone,
            FullName = request.FullName.Trim(),
            Email = email,
            // Cố định PATIENT. Điều dưỡng không có đường nào tạo vai trò khác (BR-03).
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            MustChangePassword = true,
            BiometricEnabled = false,
            // KHÁC UC-04: ở đó ngày sinh của PATIENT bị vứt bỏ vì Admin không được thấy
            // (BR-01). Ở đây người thao tác là Điều dưỡng, và ngày sinh là dữ liệu lâm sàng
            // họ cần — nó hiển thị chỉ-đọc suốt UC-06/07/08.
            DateOfBirth = request.DateOfBirth,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _users.AddAsync(user, ct);

        // BR-06 / GB-09 (PRD v1.28) — hành động tài khoản của Điều dưỡng phải truy vết được
        // y như của Admin. Cùng DbContext nên một lần SaveChanges lưu cả hai.
        await _audit.AddAsync(new AuditLog
        {
            LogId = Guid.NewGuid(),
            ActorId = actingNurseId,
            Action = "NURSE_CREATE_PATIENT_ACCOUNT",
            Detail = $"Created patient account {user.UserId} ({phone})",
            PerformedAt = now,
        }, ct);

        await _users.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Nurse {NurseId} created patient account {UserId}", actingNurseId, user.UserId);

        // Trả plaintext đúng một lần ở đây — quyết định ghi đè 06/08/2026. Điều dưỡng đọc trực tiếp
        // cho bệnh nhân, không còn kênh email nào cho mật khẩu tạm nữa.
        return new PatientAccountCreatedResponse(
            UserId: user.UserId,
            FullName: user.FullName,
            PhoneNumber: user.Phone,
            DateOfBirth: user.DateOfBirth,
            Email: user.Email,
            TemporaryPassword: temporaryPassword);
    }

    public async Task<PatientAccountResponse> UpdateContactAsync(
        Guid userId, UpdatePatientAccountRequest request, Guid actingNurseId, CancellationToken ct = default)
    {
        var user = await _users.GetForUpdateAsync(userId, ct)
            ?? throw new ResourceNotFoundException("Patient account not found.");

        // BR-03 — Điều dưỡng chỉ được chạm tài khoản Bệnh nhân. Chặn ở tầng nghiệp vụ chứ
        // không chỉ ẩn nút: ai cũng gọi thẳng API được.
        if (user.Role != UserRole.Patient)
        {
            throw new BusinessException("Only patient accounts can be edited here.");
        }

        var phone = request.PhoneNumber.Trim();
        if (!string.Equals(phone, user.Phone, StringComparison.Ordinal)
            && await _users.PhoneExistsAsync(phone, ct))
        {
            // UC-04 BR-02 vẫn nguyên vẹn — đổi số điện thoại là đổi định danh đăng nhập.
            throw new ConflictException("This phone number is already registered.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && await _users.IsEmailUsedByAnotherUserAsync(userId, email, ct))
        {
            throw new ConflictException("This email is already registered.");
        }

        // BR-04 — đúng 4 trường này, không hơn. Role và Status không xuất hiện ở đây, và
        // cũng không có trong DTO: đó là cách chắc chắn nhất để chúng không bị đổi.
        user.FullName = request.FullName.Trim();
        user.Phone = phone;
        user.DateOfBirth = request.DateOfBirth;
        user.Email = email;
        user.UpdatedAt = DateTime.UtcNow;

        await _audit.AddAsync(new AuditLog
        {
            LogId = Guid.NewGuid(),
            ActorId = actingNurseId,
            Action = "NURSE_UPDATE_PATIENT_ACCOUNT",
            Detail = $"Updated contact info of patient account {user.UserId}",
            PerformedAt = DateTime.UtcNow,
        }, ct);

        await _users.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Nurse {NurseId} updated patient account {UserId}", actingNurseId, user.UserId);

        return ToResponse(user);
    }

    public async Task<string?> ResetPasswordAsync(Guid userId, Guid actingNurseId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new ResourceNotFoundException("Patient account not found.");

        // BR-03 — chỉ tài khoản Bệnh nhân.
        if (user.Role != UserRole.Patient)
        {
            throw new BusinessException("Only patient accounts can be reset here.");
        }

        // BR-05 — dùng lại đúng cơ chế sinh-và-gửi của UC-03/UC-04, KHÔNG viết đường sinh mật
        // khẩu thứ hai. AccountHasNoEmail không còn xuất hiện ở đây nữa kể từ 06/08/2026 —
        // tài khoản không có email giờ trả về plaintext thay vì lỗi, xem AdminResetAsync.
        var outcome = await _passwordReset.AdminResetAsync(userId, actingNurseId, ct);

        if (outcome.Result != AccountOperationResult.Success)
        {
            throw new BusinessException(outcome.Result switch
            {
                AccountOperationResult.EmailNotSent =>
                    "Could not send the temporary password. The old password is still valid — please try again.",
                AccountOperationResult.AccountIsDeactivated =>
                    "This account has been deactivated.",
                _ => "Could not reset the password for this account.",
            });
        }

        await _audit.AddAsync(new AuditLog
        {
            LogId = Guid.NewGuid(),
            ActorId = actingNurseId,
            Action = "NURSE_RESET_PATIENT_PASSWORD",
            Detail = $"Reset password of patient account {userId}",
            PerformedAt = DateTime.UtcNow,
        }, ct);

        await _users.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Nurse {NurseId} reset password of patient account {UserId}", actingNurseId, userId);

        return outcome.TemporaryPassword;
    }

    private static PatientAccountResponse ToResponse(User user) => new(
        UserId: user.UserId,
        FullName: user.FullName,
        PhoneNumber: user.Phone,
        DateOfBirth: user.DateOfBirth,
        Email: user.Email);
}
