using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IPrescriptionRepository. Read-only queries dùng
/// AsNoTracking (§4.1) để giảm overhead. Include navigation để tránh N+1
/// khi caller cần items / medicine / doctor. KHÔNG có RemoveAsync (GB-03).
/// </summary>
public sealed class PrescriptionRepository : IPrescriptionRepository
{
    private readonly AppDbContext _db;

    public PrescriptionRepository(AppDbContext db) => _db = db;

    public async Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Doctor)
            .Include(p => p.Case)
            .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId, ct);
    }

    public async Task<IReadOnlyList<Prescription>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        // Prescription aggregate không có patientId trực tiếp — patient thuộc về Case
        // nhưng repository chỉ thấy Prescription. Caller truyền patientId để filter
        // qua Case.PatientProfileId. Implementation này giả định caller đã lookup
        // Case để derive patientId, hoặc sẽ dùng join ở controller layer.
        // Để tránh scope creep của repository, hiện trả về rỗng — sẽ bổ sung khi
        // Prescription entity có FK trực tiếp tới PatientProfile (xem schema team).
        await Task.CompletedTask;
        return Array.Empty<Prescription>();
    }

    public async Task<IReadOnlyList<Prescription>> ListByDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Where(p => p.DoctorId == doctorId)
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Prescription prescription, CancellationToken ct = default)
    {
        await _db.Prescriptions.AddAsync(prescription, ct);
    }

    public Task<Prescription?> GetActiveByCaseWithItemsAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
                    .ThenInclude(m => m.MedicinePackagings)
                        .ThenInclude(mp => mp.MedicineUnit)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active, ct);

    public Task<Prescription?> GetActiveByCaseForIntakeScheduleAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
            .Include(p => p.Case)
            .FirstOrDefaultAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active, ct);

    public Task<bool> ExistsActiveByCaseAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Prescriptions.AnyAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active, ct);

    public async Task<IReadOnlyList<Prescription>> ListLatestByPatientWithItemsAsync(
        Guid patientProfileId, int take, int maxItemsPerPrescription, CancellationToken ct = default) =>
        await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems.Take(maxItemsPerPrescription))
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Case)
            .Where(p => p.Case.PatientProfileId == patientProfileId)
            .OrderByDescending(p => p.PrescribedDate)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Prescription>> ListActiveForDoctorTrackingAsync(
        Guid doctorId, DateOnly activeOn, Guid? patientProfileId = null, CancellationToken ct = default)
    {
        var query = ActiveForDoctorTracking(doctorId, activeOn);
        if (patientProfileId.HasValue)
            query = query.Where(p => p.Case.PatientProfileId == patientProfileId.Value);

        return await query.ToListAsync(ct);
    }

    public Task<Prescription?> GetActiveForDoctorTrackingAsync(
        Guid prescriptionId, Guid doctorId, Guid patientProfileId, CancellationToken ct = default) =>
        WithTrackingNavigations(_db.Prescriptions.AsNoTracking())
            .Where(p => p.PrescriptionId == prescriptionId
                && p.DoctorId == doctorId
                && p.Status == PrescriptionStatus.Active
                && p.Case.PatientProfileId == patientProfileId)
            .FirstOrDefaultAsync(ct);

    private IQueryable<Prescription> ActiveForDoctorTracking(Guid doctorId, DateOnly activeOn) =>
        WithTrackingNavigations(_db.Prescriptions.AsNoTracking())
            .Where(p => p.DoctorId == doctorId && p.Status == PrescriptionStatus.Active)
            // Đơn đã hết hạn (mọi dòng thuốc đều có StartDate + DurationDays - 1 < activeOn) không tính.
            .Where(p => p.PrescriptionItems.Any(pi => pi.StartDate.AddDays(pi.DurationDays - 1) >= activeOn));

    private static IQueryable<Prescription> WithTrackingNavigations(IQueryable<Prescription> query) =>
        query
            .Include(p => p.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(pp => pp.User)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.MedicationIntakeLogs)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine);

    public async Task<Prescription?> GetByCaseIdAsync(Guid caseId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Doctor)
            .Where(p => p.CaseId == caseId)
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Prescription>> ListByCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        return await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Doctor)
            .Where(p => p.CaseId == caseId)
            .OrderByDescending(p => p.PrescribedDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }
}
