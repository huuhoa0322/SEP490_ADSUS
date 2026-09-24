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

        var now = DateTime.UtcNow;

        // Mọi tài khoản PATIENT đều có sẵn bản ghi hồ sơ rỗng (để đặt lịch được ngay, UC-13).
        // Có bản ghi chưa phải đã lập hồ sơ nền — chỉ chặn khi Bác sĩ/Điều dưỡng đã lập rồi.
        var existing = await _profiles.GetForUpdateByUserIdAsync(request.PatientUserId, ct);
        if (existing != null)
        {
            var creator = await _users.GetByIdAsync(existing.CreatedBy, ct);
            if (creator != null && PatientProfileBaseline.IsEstablishedBy(creator.Role))
            {
                throw new ConflictException("This patient already has a baseline profile.");
            }

            return await EstablishOnExistingProfileAsync(existing, patient, request, actingUserId, now, ct);
        }

        // Set Gender vào User (2026-01 - đã chuyển từ PatientProfile)
        patient.Gender = EnumExtensions.ParseGenderType(request.Gender) ?? GenderType.Female;
        patient.UpdatedAt = now;

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = request.PatientUserId,

            // Gender đã chuyển sang User entity (2026-01)
            PatientDiseases = request.Diseases?.Select(d => new PatientDisease
            {
                Id = Guid.NewGuid(),
                DiseaseId = d.DiseaseId,
                Note = d.Note,
                CreatedAt = now
            }).ToList() ?? new List<PatientDisease>(),
            PatientAllergies = request.Allergies?.Select(a => new PatientAllergy
            {
                Id = Guid.NewGuid(),
                AllergyTypeId = a.AllergyTypeId,
                Note = a.Note,
                CreatedAt = now
            }).ToList() ?? new List<PatientAllergy>(),

            // Ghi đúng người đang thao tác, kể cả khi đó là Điều dưỡng. UC-06 cho phép cả
            // Doctor lẫn Nurse lập hồ sơ; chú thích "phải là DOCTOR" trong schema chỉ là
            // COMMENT, không phải ràng buộc CHECK.
            CreatedBy = actingUserId,

            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _profiles.AddAsync(profile, ct);
            await _users.SaveChangesAsync(ct); // Lưu User.Gender đã set ở trên
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo hồ sơ nền cho người dùng {PatientUserId}. Vòng lặp hoặc thao tác CSDL gặp sự cố.", request.PatientUserId);
            throw new InvalidOperationException("Hệ thống quá tải hoặc lỗi CSDL khi tạo hồ sơ nền. Vui lòng thử lại sau.", ex);
        }
        _logger.LogInformation(
            "Patient profile {PatientProfileId} created for user {PatientUserId} by {ActingUserId}",
            profile.PatientProfileId, request.PatientUserId, actingUserId);

        // Đọc lại để lấy kèm họ tên/sđt/ngày sinh từ bảng users cho response.
        var saved = await _profiles.GetByIdAsync(profile.PatientProfileId, ct);

        return PatientProfileMapper.ToResponse(saved!);
    }

    /// <summary>
    /// UC-06 trên bản ghi hồ sơ tạo sẵn lúc tạo tài khoản: điền bệnh nền/dị ứng rồi ghi đè
    /// created_by bằng người lập — từ đó hồ sơ mới được tính là đã lập (PatientProfileBaseline).
    /// </summary>
    private async Task<PatientProfileResponse> EstablishOnExistingProfileAsync(
        PatientProfile profile,
        User patient,
        CreatePatientProfileRequest request,
        Guid actingUserId,
        DateTime now,
        CancellationToken ct)
    {
        patient.Gender = EnumExtensions.ParseGenderType(request.Gender) ?? GenderType.Female;
        patient.UpdatedAt = now;

        profile.PatientDiseases.Clear();
        foreach (var d in request.Diseases ?? Enumerable.Empty<PatientDiseaseInput>())
        {
            profile.PatientDiseases.Add(new PatientDisease
            {
                Id = Guid.NewGuid(),
                DiseaseId = d.DiseaseId,
                Note = d.Note,
                CreatedAt = now
            });
        }

        profile.PatientAllergies.Clear();
        foreach (var a in request.Allergies ?? Enumerable.Empty<PatientAllergyInput>())
        {
            profile.PatientAllergies.Add(new PatientAllergy
            {
                Id = Guid.NewGuid(),
                AllergyTypeId = a.AllergyTypeId,
                Note = a.Note,
                CreatedAt = now
            });
        }

        profile.CreatedBy = actingUserId;
        profile.UpdatedAt = now;

        try
        {
            await _profiles.UpdateAsync(profile, ct);
            await _users.SaveChangesAsync(ct); // Lưu User.Gender đã set ở trên
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lập hồ sơ nền cho người dùng {PatientUserId}. Vòng lặp hoặc thao tác CSDL gặp sự cố.", request.PatientUserId);
            throw new InvalidOperationException("Hệ thống quá tải hoặc lỗi CSDL khi tạo hồ sơ nền. Vui lòng thử lại sau.", ex);
        }
        _logger.LogInformation(
            "Baseline established on existing patient profile {PatientProfileId} for user {PatientUserId} by {ActingUserId}",
            profile.PatientProfileId, request.PatientUserId, actingUserId);

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

        var now = DateTime.UtcNow;

        // Set Gender vào User (2026-01 - đã chuyển từ PatientProfile)
        if (request.Gender != null && profile.User != null)
        {
            profile.User.Gender = EnumExtensions.ParseGenderType(request.Gender);
            profile.User.UpdatedAt = now;
        }

        profile.PatientDiseases.Clear();
        if (request.Diseases != null)
        {
            foreach (var d in request.Diseases)
            {
                profile.PatientDiseases.Add(new PatientDisease
                {
                    DiseaseId = d.DiseaseId,
                    Note = d.Note,
                    CreatedAt = now
                });
            }
        }

        profile.PatientAllergies.Clear();
        if (request.Allergies != null)
        {
            foreach (var a in request.Allergies)
            {
                profile.PatientAllergies.Add(new PatientAllergy
                {
                    AllergyTypeId = a.AllergyTypeId,
                    Note = a.Note,
                    CreatedAt = now
                });
            }
        }

        profile.UpdatedAt = now;

        try
        {
            await _profiles.UpdateAsync(profile, ct);
            await _users.SaveChangesAsync(ct); // Lưu User.Gender đã set ở trên
            _logger.LogInformation("Patient profile {PatientProfileId} updated", patientProfileId);
            return PatientProfileMapper.ToResponse(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật hồ sơ nền {PatientProfileId}. Vòng lặp hoặc thao tác CSDL gặp sự cố.", patientProfileId);
            throw new InvalidOperationException("Hệ thống quá tải hoặc lỗi CSDL khi lưu hồ sơ. Vui lòng thử lại sau.", ex);
        }
    }

    public async Task<PatientProfileResponse> GetByIdAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetByIdAsync(patientProfileId, ct)
            ?? throw new ResourceNotFoundException("Patient profile not found.");

        return PatientProfileMapper.ToResponse(profile);
    }

    public async Task<PatientProfileResponse?> FindByIdAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetByIdAsync(patientProfileId, ct);
        return profile is null ? null : PatientProfileMapper.ToResponse(profile);
    }

    public async Task<PagedResult<PatientSummaryResponse>> SearchPatientsAsync(
        string? search,
        string? visitStatus,
        bool? hasProfile,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (rows, total) = await _profiles.SearchAsync(search, visitStatus, hasProfile, page, pageSize, ct);

        var items = rows
            .Select(r => new PatientSummaryResponse(
                PatientProfileId: r.PatientProfileId,
                PatientUserId: r.PatientUserId,
                FullName: r.FullName,
                Phone: r.Phone,
                LatestVisitDate: r.LatestVisitDate,
                LatestVisitStatus: r.LatestVisitStatus,
                LatestCaseId: r.LatestCaseId,
                HasBaselineProfile: r.HasBaselineProfile))
            .ToList();

        // Trang 0 buộc giao diện phải xử lý riêng một trường hợp vô nghĩa, nên tối thiểu là 1.
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);

        return new PagedResult<PatientSummaryResponse>(items, page, pageSize, total, totalPages);
    }
}
