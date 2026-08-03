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
            .FirstOrDefaultAsync(m => m.Name == trimmed, ct);
    }

    public async Task<IReadOnlyList<Medicine>> SearchAsync(string keyword, int max, CancellationToken ct = default)
    {
        var kw = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(kw)) return Array.Empty<Medicine>();

        var lower = kw.ToLowerInvariant();
        return await _db.Medicines
            .AsNoTracking()
            .Where(m => m.Name.ToLower().Contains(lower))
            .OrderBy(m => m.Name)
            .Take(Math.Clamp(max, 1, 50))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Medicine medicine, CancellationToken ct = default)
    {
        await _db.Medicines.AddAsync(medicine, ct);
    }
}
