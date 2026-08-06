using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của ICaseRepository (UC-07, UC-08, UC-12).
/// KHÔNG có Remove: ca bệnh và ảnh là bằng chứng y tế, GB-03 cấm xoá.
/// </summary>
public sealed class CaseRepository : ICaseRepository
{
    private readonly AppDbContext _db;

    public CaseRepository(AppDbContext db) => _db = db;

    public Task<Case?> GetDetailAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Cases
            .AsNoTracking()
            // AsSplitQuery: Case có 4 nhánh collection. Gộp chung một câu SQL sẽ nhân chéo
            // số dòng (5 ảnh × 3 finding × 4 thuốc = 60 dòng cho một ca).
            .AsSplitQuery()
            .Include(c => c.PatientProfile).ThenInclude(p => p.User)
            .Include(c => c.Doctor)
            .Include(c => c.UltrasoundImages)
            .Include(c => c.Prescriptions).ThenInclude(p => p.PrescriptionItems).ThenInclude(i => i.Medicine)
            .FirstOrDefaultAsync(c => c.CaseId == caseId, ct);

    public Task<Case?> GetByIdAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CaseId == caseId, ct);

    public async Task<(IReadOnlyList<Case> Items, int TotalCount)> SearchByPatientAsync(
        Guid patientProfileId,
        CaseStatus? status,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Cases
            .AsNoTracking()
            .Where(c => c.PatientProfileId == patientProfileId);

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var total = await query.CountAsync(ct);

        query = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(c => c.VisitDate).ThenBy(c => c.CreatedAt)
            : query.OrderByDescending(c => c.VisitDate).ThenByDescending(c => c.CreatedAt);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Case> CreateWithImagesAsync(
        Case newCase,
        IReadOnlyList<UltrasoundImage> images,
        CancellationToken ct = default)
    {
        _db.Cases.Add(newCase);
        _db.UltrasoundImages.AddRange(images);

        // Một SaveChanges cho cả hai bảng — EF gói trong một transaction, nên ca bệnh và ảnh
        // cùng ghi được hoặc cùng không.
        await _db.SaveChangesAsync(ct);

        return newCase;
    }
}
