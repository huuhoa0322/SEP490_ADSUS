using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// SCR-19 — reminder settings của bệnh nhân.
/// Lấy patientProfileId từ userId qua PatientProfiles.UserId → PatientProfileId.
/// Nếu bệnh nhân chưa có dòng preference thì trả default (không tạo row mới cho GET).
/// Upsert tạo row mới nếu chưa có.
/// </summary>
public sealed class ReminderPreferenceService : IReminderPreferenceService
{
    /// <summary>Mặc định khi bệnh nhân chưa có dòng preference.</summary>
    private static readonly ReminderPreferenceResponse Default = new(
        NotifEnabled: true,
        MorningTime: "07:00",
        MiddayTime: "12:00",
        EveningTime: "20:00");

    private readonly AppDbContext _db;
    private readonly IReminderPreferenceRepository _prefRepo;

    public ReminderPreferenceService(
        AppDbContext db,
        IReminderPreferenceRepository prefRepo)
    {
        _db = db;
        _prefRepo = prefRepo;
    }

    public async Task<ReminderPreferenceResponse> GetAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Hồ sơ bệnh nhân không tồn tại.");

        var pref = await _prefRepo.GetByPatientProfileIdAsync(
            patientProfile.PatientProfileId, ct);

        if (pref is null)
            return Default;

        return new ReminderPreferenceResponse(
            NotifEnabled: pref.NotifEnabled ?? true,
            MorningTime: ToHHmm(pref.MorningTime ?? new TimeOnly(7, 0)),
            MiddayTime: ToHHmm(pref.MiddayTime ?? new TimeOnly(12, 0)),
            EveningTime: ToHHmm(pref.EveningTime ?? new TimeOnly(20, 0)));
    }

    public async Task<ReminderPreferenceResponse> UpsertAsync(
        Guid userId,
        UpdateReminderPreferenceRequest request,
        CancellationToken ct = default)
    {
        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Hồ sơ bệnh nhân không tồn tại.");

        var existing = await _prefRepo.GetForUpdateAsync(
            patientProfile.PatientProfileId, ct);

        if (existing is null)
        {
            // Tạo mới với default + override từ request
            existing = new PatientReminderPreference
            {
                PreferenceId = Guid.NewGuid(),
                PatientProfileId = patientProfile.PatientProfileId,
                NotifEnabled = request.NotifEnabled ?? true,
                MorningTime = ParseOrDefault(request.MorningTime, new TimeOnly(7, 0)),
                MiddayTime = ParseOrDefault(request.MiddayTime, new TimeOnly(12, 0)),
                EveningTime = ParseOrDefault(request.EveningTime, new TimeOnly(20, 0)),
            };
            await _prefRepo.AddAsync(existing, ct);
        }
        else
        {
            // Override chỉ trường được gửi lên, giữ nguyên những thứ khác
            if (request.NotifEnabled.HasValue)
                existing.NotifEnabled = request.NotifEnabled.Value;
            if (request.MorningTime is not null)
                existing.MorningTime = ParseTimeOnly(request.MorningTime);
            if (request.MiddayTime is not null)
                existing.MiddayTime = ParseTimeOnly(request.MiddayTime);
            if (request.EveningTime is not null)
                existing.EveningTime = ParseTimeOnly(request.EveningTime);

            await _prefRepo.UpdateAsync(existing, ct);
        }

        await _db.SaveChangesAsync(ct);

        return new ReminderPreferenceResponse(
            NotifEnabled: existing.NotifEnabled ?? true,
            MorningTime: ToHHmm(existing.MorningTime ?? new TimeOnly(7, 0)),
            MiddayTime: ToHHmm(existing.MiddayTime ?? new TimeOnly(12, 0)),
            EveningTime: ToHHmm(existing.EveningTime ?? new TimeOnly(20, 0)));
    }

    private static string ToHHmm(TimeOnly t) =>
        $"{t.Hour:D2}:{t.Minute:D2}";

    private static TimeOnly ParseTimeOnly(string value)
    {
        if (TimeOnly.TryParse(value, out var result))
            return result;
        throw new ArgumentException($"Định dạng giờ không hợp lệ: '{value}'. Dùng HH:mm (ví dụ: 07:30).");
    }

    private static TimeOnly ParseOrDefault(string? value, TimeOnly default_)
    {
        if (string.IsNullOrWhiteSpace(value))
            return default_;
        return ParseTimeOnly(value);
    }
}
