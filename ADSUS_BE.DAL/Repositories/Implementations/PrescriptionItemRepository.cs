using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IPrescriptionItemRepository. ScheduleSlots KHÔNG
/// persist ở entity này (master convention) — duration_days + start_date đủ để
/// IntakeLogGenerationService tính ra lịch uống kết hợp patient_reminder_preferences.
/// </summary>
public sealed class PrescriptionItemRepository : IPrescriptionItemRepository
{
    private readonly AppDbContext _db;

    public PrescriptionItemRepository(AppDbContext db) => _db = db;

    public async Task<PrescriptionItem?> GetByIdAsync(Guid prescriptionItemId, CancellationToken ct = default)
    {
        return await _db.PrescriptionItems
            .AsNoTracking()
            .Include(pi => pi.Medicine)
            .Include(pi => pi.Prescription)
            .Include(pi => pi.MedicationIntakeLogs)
            .FirstOrDefaultAsync(pi => pi.PrescriptionItemId == prescriptionItemId, ct);
    }

    public async Task<IReadOnlyList<PrescriptionItem>> ListByPrescriptionAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        return await _db.PrescriptionItems
            .AsNoTracking()
            .Include(pi => pi.Medicine)
            .Where(pi => pi.PrescriptionId == prescriptionId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PrescriptionItem>> ListByPrescriptionIdsAsync(
        IReadOnlyCollection<Guid> prescriptionIds,
        CancellationToken ct = default)
    {
        if (prescriptionIds.Count == 0) return Array.Empty<PrescriptionItem>();
        return await _db.PrescriptionItems
            .AsNoTracking()
            .Include(pi => pi.Medicine)
            .Where(pi => prescriptionIds.Contains(pi.PrescriptionId))
            .ToListAsync(ct);
    }

    public async Task AddAsync(PrescriptionItem item, CancellationToken ct = default)
    {
        await _db.PrescriptionItems.AddAsync(item, ct);
    }

    public async Task AddRangeAsync(IEnumerable<PrescriptionItem> items, CancellationToken ct = default)
    {
        await _db.PrescriptionItems.AddRangeAsync(items, ct);
    }
}
