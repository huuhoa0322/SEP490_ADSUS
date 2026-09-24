using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của IClinicServiceRepository.</summary>
public sealed class ClinicServiceRepository : IClinicServiceRepository
{
    private readonly AppDbContext _db;

    public ClinicServiceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClinicService>> ListAsync(bool? isActive, CancellationToken ct = default)
    {
        var query = _db.ClinicServices.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        return await query.OrderBy(s => s.Code).ToListAsync(ct);
    }

    public Task<ClinicService?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ClinicServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<ClinicService?> FindActiveByCodeAsync(string code, CancellationToken ct = default) =>
        _db.ClinicServices.AsNoTracking().FirstOrDefaultAsync(s => s.Code == code && s.IsActive, ct);

    public Task<ClinicService?> GetForUpdateAsync(Guid id, CancellationToken ct = default) =>
        _db.ClinicServices.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> CodeExistsAsync(string upperCode, CancellationToken ct = default) =>
        _db.ClinicServices.AnyAsync(s => s.Code.ToUpper() == upperCode, ct);

    public async Task AddAsync(ClinicService service, CancellationToken ct = default) =>
        await _db.ClinicServices.AddAsync(service, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
