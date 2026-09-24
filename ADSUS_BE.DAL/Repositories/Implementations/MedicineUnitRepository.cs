using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của IMedicineUnitRepository.</summary>
public sealed class MedicineUnitRepository : IMedicineUnitRepository
{
    private readonly AppDbContext _db;

    public MedicineUnitRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MedicineUnit>> ListAsync(CancellationToken ct = default) =>
        await _db.MedicineUnits
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .ToListAsync(ct);
}
