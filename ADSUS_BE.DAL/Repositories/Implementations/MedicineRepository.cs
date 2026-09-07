using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IMedicineRepository. Catalog tra cứu khi bác sĩ gõ
/// tên thuốc trong ô tìm kiếm (xem comment PrescriptionItem master).
/// </summary>
public sealed class MedicineRepository : IMedicineRepository
{
    private readonly AppDbContext _db;

    public MedicineRepository(AppDbContext db) => _db = db;

    public async Task<Medicine?> GetByIdAsync(Guid medicineId, CancellationToken ct = default)
    {
        return await _db.Medicines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MedicineId == medicineId, ct);
    }

    public async Task<Medicine?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var trimmed = name.Trim();
        return await _db.Medicines
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                EF.Functions.ILike(m.Name, trimmed), ct);
    }

    public async Task AddAsync(Medicine medicine, CancellationToken ct = default)
    {
        await _db.Medicines.AddAsync(medicine, ct);
    }

    public async Task<IReadOnlyList<Medicine>> ListAllAsync(CancellationToken ct = default)
    {
        return await _db.Medicines
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Medicine>> SearchByNameAsync(string keyword, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return await _db.Medicines
                .Include(m => m.MedicineBatches)
                .AsNoTracking()
                .Where(m => m.Status == MedicineStatus.Active)
                .OrderBy(m => m.Name)
                .Take(limit)
                .ToListAsync(ct);
        }

        var trimmed = keyword.Trim();
        return await _db.Medicines
            .Include(m => m.MedicineBatches)
            .AsNoTracking()
            .Where(m => m.Status == MedicineStatus.Active && EF.Functions.ILike(m.Name, $"%{trimmed}%"))
            .OrderBy(m => m.Name)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task UpdateAsync(Medicine medicine, CancellationToken ct = default)
    {
        _db.Medicines.Update(medicine);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<Medicine> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword, bool? inStock = null, CancellationToken ct = default)
    {
        var query = _db.Medicines.Include(m => m.MedicineBatches).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(m => EF.Functions.ILike(m.Name, $"%{trimmed}%"));
        }

        if (inStock.HasValue)
        {
            if (inStock.Value)
            {
                query = query.Where(m => m.MedicineBatches.Sum(b => b.QuantityBase) > 0);
            }
            else
            {
                query = query.Where(m => m.MedicineBatches.Sum(b => b.QuantityBase) == 0);
            }
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> HasBeenPrescribedAsync(Guid medicineId, CancellationToken ct = default)
    {
        return await _db.PrescriptionItems
            .AsNoTracking()
            .AnyAsync(pi => pi.MedicineId == medicineId, ct);
    }
}

