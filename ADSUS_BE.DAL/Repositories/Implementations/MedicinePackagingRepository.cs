using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>EF Core implementation của IMedicinePackagingRepository.</summary>
public sealed class MedicinePackagingRepository : IMedicinePackagingRepository
{
    private readonly AppDbContext _db;

    public MedicinePackagingRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, string>> GetBaseUnitNamesAsync(
        IReadOnlyCollection<Guid> medicineIds,
        CancellationToken ct = default)
    {
        return await _db.MedicinePackagings
            .AsNoTracking()
            .Where(mp => medicineIds.Contains(mp.MedicineId) && mp.IsBaseUnit)
            .Select(mp => new { mp.MedicineId, mp.MedicineUnit.Name })
            .ToDictionaryAsync(x => x.MedicineId, x => x.Name, ct);
    }

    public Task<string?> GetBaseUnitNameAsync(Guid medicineId, CancellationToken ct = default) =>
        _db.MedicinePackagings
            .AsNoTracking()
            .Where(mp => mp.MedicineId == medicineId && mp.IsBaseUnit)
            .Select(mp => (string?)mp.MedicineUnit.Name)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<MedicinePackaging>> ListByMedicineAsync(Guid medicineId, CancellationToken ct = default)
    {
        return await _db.MedicinePackagings
            .AsNoTracking()
            .Include(p => p.MedicineUnit)
            .Where(p => p.MedicineId == medicineId)
            .OrderByDescending(p => p.IsBaseUnit)
            .ThenBy(p => p.ConversionFactor)
            .ToListAsync(ct);
    }

    public Task<MedicinePackaging?> GetByIdAsync(Guid packagingId, CancellationToken ct = default) =>
        _db.MedicinePackagings.AsNoTracking().FirstOrDefaultAsync(p => p.Id == packagingId, ct);

    public Task<MedicinePackaging?> GetWithUnitAsync(Guid packagingId, CancellationToken ct = default) =>
        _db.MedicinePackagings
            .AsNoTracking()
            .Include(p => p.MedicineUnit)
            .FirstOrDefaultAsync(p => p.Id == packagingId, ct);

    public Task<MedicinePackaging?> GetForUpdateAsync(Guid packagingId, CancellationToken ct = default) =>
        _db.MedicinePackagings.FirstOrDefaultAsync(p => p.Id == packagingId, ct);

    public Task<bool> ExistsForUnitAsync(
        Guid medicineId,
        Guid medicineUnitId,
        Guid? excludePackagingId,
        CancellationToken ct = default)
    {
        var query = _db.MedicinePackagings
            .Where(p => p.MedicineId == medicineId && p.MedicineUnitId == medicineUnitId);
        if (excludePackagingId.HasValue)
            query = query.Where(p => p.Id != excludePackagingId.Value);
        return query.AnyAsync(ct);
    }

    public Task<bool> IsUsedInInventoryAsync(Guid packagingId, CancellationToken ct = default) =>
        _db.InventoryTransactions.AnyAsync(t => t.MedicinePackagingId == packagingId, ct);

    public async Task AddAsync(MedicinePackaging packaging, CancellationToken ct = default) =>
        await _db.MedicinePackagings.AddAsync(packaging, ct);

    public void Remove(MedicinePackaging packaging) => _db.MedicinePackagings.Remove(packaging);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
