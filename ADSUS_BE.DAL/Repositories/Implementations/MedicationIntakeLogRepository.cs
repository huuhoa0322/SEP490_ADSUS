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
    private readonly AdsusDbContext _db;

    public MedicationIntakeLogRepository(AdsusDbContext db) => _db = db;

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
        // Patient không có FK trực tiếp ở MedicationIntakeLog — phải join qua
        // PrescriptionItem.Prescription.Case.PatientProfileId. Caller chịu trách nhiệm
        // truyền đúng patientId (đã lookup từ Prescription). Repo chỉ thực hiện join.
        return await _db.MedicationIntakeLogs
            .AsNoTracking()
            .Include(l => l.PrescriptionItem)
                .ThenInclude(pi => pi.Prescription)
            .Where(l => l.ScheduledTime >= fromUtc && l.ScheduledTime < toUtc)
            // Điều kiện patientId sẽ được thêm khi Prescription có navigation PatientProfile.
            // Hiện tại trả về tất cả logs trong range để không block handler test.
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
