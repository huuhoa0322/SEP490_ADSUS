using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Mappers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

/// <summary>
/// UC-06 — hồ sơ y tế nền của bệnh nhân (SCR-10).
/// </summary>
public sealed class PatientProfileService : IPatientProfileService
{
    private readonly IPatientProfileRepository _profiles;
    private readonly IUserRepository _users;
    private readonly ILogger<PatientProfileService> _logger;

    public PatientProfileService(
        IPatientProfileRepository profiles,
        IUserRepository users,
        ILogger<PatientProfileService> logger)
    {
        _profiles = profiles;
        _users = users;
        _logger = logger;
    }

    public async Task<PatientProfileResponse> CreateAsync(
        CreatePatientProfileRequest request,
        Guid actingUserId,
        CancellationToken ct = default)
    {
        var patient = await _users.GetByIdAsync(request.PatientUserId, ct)
            ?? throw new ResourceNotFoundException("Patient account not found.");

        // BR-01: hồ sơ nền chỉ gắn với tài khoản có vai trò PATIENT.
        if (patient.Role != UserRole.Patient)
        {
            throw new BusinessException("The selected account is not a patient account.");
        }

        if (await _profiles.ExistsForUserAsync(request.PatientUserId, ct))
        {
            throw new ConflictException("This patient already has a baseline profile.");
        }

        var now = DateTime.UtcNow;

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = request.PatientUserId,

            // Validator đã bảo đảm chuỗi này đọc được nếu có gửi. Không gửi gì thì lấy đúng
            // mặc định của cột DB — nhưng giao diện vẫn nên luôn gửi giá trị rõ ràng.
            Gender = EnumExtensions.ParseGenderType(request.Gender) ?? GenderType.Female,

            MedicalHistory = request.MedicalHistory,
            Allergies = request.Allergies,

            // Ghi đúng người đang thao tác, kể cả khi đó là Điều dưỡng. UC-06 cho phép cả
            // Doctor lẫn Nurse lập hồ sơ; chú thích "phải là DOCTOR" trong schema chỉ là
            // COMMENT, không phải ràng buộc CHECK.
            CreatedBy = actingUserId,

            CreatedAt = now,
            UpdatedAt = now,
        };

        await _profiles.AddAsync(profile, ct);

        _logger.LogInformation(
            "Patient profile {PatientProfileId} created for user {PatientUserId} by {ActingUserId}",
            profile.PatientProfileId, request.PatientUserId, actingUserId);

        // Đọc lại để lấy kèm họ tên/sđt/ngày sinh từ bảng users cho response.
        var saved = await _profiles.GetByIdAsync(profile.PatientProfileId, ct);

        return PatientProfileMapper.ToResponse(saved!);
    }

    public async Task<PatientProfileResponse> UpdateAsync(
        Guid patientProfileId,
        UpdatePatientProfileRequest request,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetForUpdateAsync(patientProfileId, ct)
            ?? throw new ResourceNotFoundException("Patient profile not found.");

        profile.Gender = EnumExtensions.ParseGenderType(request.Gender) ?? GenderType.Female;
        profile.MedicalHistory = request.MedicalHistory;
        profile.Allergies = request.Allergies;
        profile.UpdatedAt = DateTime.UtcNow;

        await _profiles.UpdateAsync(profile, ct);

        _logger.LogInformation("Patient profile {PatientProfileId} updated", patientProfileId);

        return PatientProfileMapper.ToResponse(profile);
    }

    public async Task<PatientProfileResponse> GetByIdAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetByIdAsync(patientProfileId, ct)
            ?? throw new ResourceNotFoundException("Patient profile not found.");

        return PatientProfileMapper.ToResponse(profile);
    }
}
