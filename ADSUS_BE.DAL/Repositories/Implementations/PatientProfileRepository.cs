using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// Module 7 — UC-11: lookup tên bệnh nhân ở tầng service để nhúng vào
/// PrescriptionDetailResponse. Chỉ đọc.
/// </summary>
public sealed class PatientProfileRepository : IPatientProfileRepository
{
    private readonly AppDbContext _db;

    public PatientProfileRepository(AppDbContext db) => _db = db;

    public async Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.PatientProfiles.FindAsync(new object?[] { id }, ct);
    }
}
