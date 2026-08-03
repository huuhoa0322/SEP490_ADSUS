using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IPatientProfileRepository (UC-06, UC-09).
/// KHÔNG có Remove: GB-03 cấm xoá dữ liệu y tế.
/// </summary>
public sealed class PatientProfileRepository : IPatientProfileRepository
{
    private readonly AppDbContext _db;

    public PatientProfileRepository(AppDbContext db) => _db = db;

    public Task<PatientProfile?> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);

    public Task<PatientProfile?> GetForUpdateAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);

    public Task<PatientProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles.AnyAsync(p => p.UserId == userId, ct);

    public async Task<PatientProfile> AddAsync(PatientProfile profile, CancellationToken ct = default)
    {
        _db.PatientProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateAsync(PatientProfile profile, CancellationToken ct = default)
    {
        _db.PatientProfiles.Update(profile);
        await _db.SaveChangesAsync(ct);
    }
}
