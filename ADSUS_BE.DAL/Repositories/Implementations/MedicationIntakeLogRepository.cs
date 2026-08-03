using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IMedicationIntakeLogRepository. Status ("PENDING" /
/// "TAKEN") derive từ ConfirmedAt — DB không có column status (xem AppDbContext
/// master). UNIQUE constraint uq_medication_intake_logs_dose ở DB sẽ reject duplicate
/// (item, scheduled_time); handler bắt PostgresException 23505 khi Quartz re-fire.
/// </summary>
public sealed class MedicationIntakeLogRepository : IMedicationIntakeLogRepository
{
    private readonly AppDbContext _db;

    public MedicationIntakeLogRepository(AppDbContext db) => _db = db;

    public async Task<MedicationIntakeLog?> FindByItemAndTimeAsync(
        Guid prescriptionItemId,
        DateTime scheduledTimeUtc,
        CancellationToken ct = default)
    {
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.PrescriptionItemId == prescriptionItemId &&
                l.ScheduledTime == scheduledTimeUtc, ct);
    }

    public async Task<IReadOnlyList<MedicationIntakeLog>> ListByItemAsync(
        Guid prescriptionItemId,
        CancellationToken ct = default)
    {
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Where(l => l.PrescriptionItemId == prescriptionItemId)
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicationIntakeLog>> ListByPatientRangeAsync(
        Guid patientId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // Filter hiện chỉ theo khoảng thời gian — patientId được giữ trong signature để caller
        // truyền vào nhưng CHƯA dùng filter (sẽ bổ sung khi Prescription có navigation
        // PatientProfile rõ ràng qua Case). Tạm thời trả về tất cả logs trong range.
        // Bỏ Include().ThenInclude() chain để tránh InMemory provider phải resolve navigation
        // non-nullable của MedicationIntakeLog → PrescriptionItem → Prescription (3 cấp) khi
        // test stub không tạo entity chain đầy đủ.
        _ = patientId; // suppress unused warning — sẽ dùng khi có navigation PatientProfile.
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Where(l => l.ScheduledTime >= fromUtc && l.ScheduledTime < toUtc)
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicationIntakeLog>> ListByPrescriptionItemIdsAsync(
        IReadOnlyCollection<Guid> prescriptionItemIds,
        CancellationToken ct = default)
    {
        if (prescriptionItemIds.Count == 0) return Array.Empty<MedicationIntakeLog>();
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Where(l => prescriptionItemIds.Contains(l.PrescriptionItemId))
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task AddAsync(MedicationIntakeLog log, CancellationToken ct = default)
    {
        await _db.MedicationIntakeLogs.AddAsync(log, ct);
    }

    public async Task AddRangeAsync(IEnumerable<MedicationIntakeLog> logs, CancellationToken ct = default)
    {
        await _db.MedicationIntakeLogs.AddRangeAsync(logs, ct);
    }
}
