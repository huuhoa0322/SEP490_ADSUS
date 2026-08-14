using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IReminderPreferenceRepository.
/// Mỗi bệnh nhân có tối đa 1 dòng — unique index trên patient_profile_id.
/// </summary>
public sealed class ReminderPreferenceRepository : IReminderPreferenceRepository
{
    private readonly AppDbContext _db;

    public ReminderPreferenceRepository(AppDbContext db) => _db = db;

    public async Task<PatientReminderPreference?> GetByPatientProfileIdAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        return await _db.PatientReminderPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);
    }

    public async Task<PatientReminderPreference?> GetForUpdateAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        return await _db.PatientReminderPreferences
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);
    }

    public async Task AddAsync(PatientReminderPreference preference, CancellationToken ct = default)
    {
        await _db.PatientReminderPreferences.AddAsync(preference, ct);
    }

    public async Task UpdateAsync(PatientReminderPreference preference, CancellationToken ct = default)
    {
        _db.PatientReminderPreferences.Update(preference);
        await Task.CompletedTask;
    }
}
