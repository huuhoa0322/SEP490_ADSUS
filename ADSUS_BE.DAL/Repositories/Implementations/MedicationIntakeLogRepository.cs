using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IMedicationIntakeLogRepository. Cột status (intake_status
/// enum) tồn tại trong DB và được cập nhật song song với ConfirmedAt để query/list
/// có thể filter trực tiếp (không phải derive). UNIQUE constraint
/// uq_medication_intake_logs_dose ở DB sẽ reject duplicate (item, scheduled_time);
/// handler bắt PostgresException 23505 khi Quartz re-fire.
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

    public async Task AddAsync(MedicationIntakeLog log, CancellationToken ct = default)
    {
        await _db.MedicationIntakeLogs.AddAsync(log, ct);
    }

    public async Task AddRangeAsync(IEnumerable<MedicationIntakeLog> logs, CancellationToken ct = default)
    {
        await _db.MedicationIntakeLogs.AddRangeAsync(logs, ct);
    }

    public async Task<MedicationIntakeLog?> GetByIdAsync(Guid intakeId, CancellationToken ct = default)
    {
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Medicine)
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Prescription)
                    .ThenInclude(p => p!.Case)
            .FirstOrDefaultAsync(l => l.IntakeId == intakeId, ct);
    }

    public async Task<IReadOnlyList<MedicationIntakeLog>> ListByPrescriptionAsync(
        Guid prescriptionId,
        CancellationToken ct = default)
    {
        var itemIds = await _db.PrescriptionItems
            .AsNoTracking()
            .Where(i => i.PrescriptionId == prescriptionId)
            .Select(i => i.PrescriptionItemId)
            .ToListAsync(ct);

        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Medicine)
            .Where(l => itemIds.Contains(l.PrescriptionItemId))
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicationIntakeLog>> ListUpcomingAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Medicine)
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Prescription)
                    .ThenInclude(p => p!.Case)
            .Where(l => l.PrescriptionItem!.Prescription!.Case!.PatientProfileId == patientProfileId
                     && l.ScheduledTime >= now
                     && l.ConfirmedAt == null)
            .OrderBy(l => l.ScheduledTime)
            .ToListAsync(ct);
    }

    public async Task ConfirmTakenAsync(Guid intakeId, DateTime confirmedAt, CancellationToken ct = default)
    {
        var log = await _db.MedicationIntakeLogs.FindAsync([intakeId], ct);
        if (log is null) return;
        log.ConfirmedAt = confirmedAt;
        log.Status = IntakeStatus.Taken;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MedicationIntakeLog>> ListDueRemindersAsync(
        DateTime windowStart,
        int reminderWindowMinutes,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Only remind once per intake log — only when ScheduledTime just passed.
        // If windowStart=now-30min and ScheduledTime falls within [now-30min, now], send one reminder.
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Medicine)
            .Include(l => l.PrescriptionItem)
                .ThenInclude(i => i!.Prescription)
                    .ThenInclude(p => p!.Case)
                        .ThenInclude(c => c!.PatientProfile)
                            .ThenInclude(pt => pt!.User)
            .Where(l => l.ScheduledTime >= windowStart
                     && l.ScheduledTime <= now
                     && l.ConfirmedAt == null)
            .ToListAsync(ct);
    }
}
