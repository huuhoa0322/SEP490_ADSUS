using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IPrescriptionRepository. Read-only queries dùng
/// AsNoTracking (§4.1) để giảm overhead. Include navigation để tránh N+1
/// khi caller cần items / medicine / doctor. KHÔNG có RemoveAsync (GB-03).
/// </summary>
public sealed class PrescriptionRepository : IPrescriptionRepository
{
    private readonly AppDbContext _db;

    public PrescriptionRepository(AppDbContext db) => _db = db;

    public async Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Doctor)
            .Include(p => p.Case)
                .ThenInclude(c => c!.PatientProfile)
            .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId, ct);
    }

    public async Task<IReadOnlyList<Prescription>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Doctor)
            .Where(p => p.Case!.PatientProfileId == patientId)
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Prescription>> ListByPatientPagedAsync(
        Guid patientProfileId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<PrescriptionStatus>? statuses,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.Doctor)
            .Where(p => p.Case!.PatientProfileId == patientProfileId);

        if (fromDate.HasValue) query = query.Where(p => p.PrescribedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(p => p.PrescribedDate <= toDate.Value);
        if (statuses is { Count: > 0 }) query = query.Where(p => statuses.Contains(p.Status));

        return await query
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountByPatientAsync(
        Guid patientProfileId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<PrescriptionStatus>? statuses,
        CancellationToken ct = default)
    {
        var query = _db.Prescriptions
            .AsNoTracking()
            .Where(p => p.Case!.PatientProfileId == patientProfileId);

        if (fromDate.HasValue) query = query.Where(p => p.PrescribedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(p => p.PrescribedDate <= toDate.Value);
        if (statuses is { Count: > 0 }) query = query.Where(p => statuses.Contains(p.Status));

        return await query.CountAsync(ct);
    }

    public async Task<bool> HasActiveForCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .AnyAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active, ct);
    }

    public async Task<IReadOnlyList<Prescription>> ListByDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Where(p => p.DoctorId == doctorId)
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Prescription prescription, CancellationToken ct = default)
    {
        await _db.Prescriptions.AddAsync(prescription, ct);
    }
}
