using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của ICaseClinicServiceRepository.</summary>
public sealed class CaseClinicServiceRepository : ICaseClinicServiceRepository
{
    private readonly AppDbContext _db;

    public CaseClinicServiceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CaseClinicService>> ListByCaseAsync(Guid caseId, CancellationToken ct = default) =>
        await _db.CaseClinicServices
            .AsNoTracking()
            .Include(cs => cs.ClinicService)
            .Where(cs => cs.CaseId == caseId)
            .OrderBy(cs => cs.CreatedAt)
            .ToListAsync(ct);

    public Task<CaseClinicService?> GetByCaseAndServiceAsync(Guid caseId, Guid clinicServiceId, CancellationToken ct = default) =>
        _db.CaseClinicServices
            .AsNoTracking()
            .Include(cs => cs.ClinicService)
            .FirstOrDefaultAsync(cs => cs.CaseId == caseId && cs.ClinicServiceId == clinicServiceId, ct);

    public Task<CaseClinicService?> GetForUpdateAsync(Guid caseClinicServiceId, CancellationToken ct = default) =>
        _db.CaseClinicServices
            .Include(cs => cs.ClinicService)
            .FirstOrDefaultAsync(cs => cs.Id == caseClinicServiceId, ct);

    public async Task AddAsync(CaseClinicService caseClinicService, CancellationToken ct = default) =>
        await _db.CaseClinicServices.AddAsync(caseClinicService, ct);

    public void Remove(CaseClinicService caseClinicService) => _db.CaseClinicServices.Remove(caseClinicService);
}
