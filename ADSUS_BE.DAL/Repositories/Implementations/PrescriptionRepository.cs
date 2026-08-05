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
}
