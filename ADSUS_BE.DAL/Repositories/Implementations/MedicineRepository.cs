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
}
