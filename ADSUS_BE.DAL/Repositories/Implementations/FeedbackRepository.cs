using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IFeedbackRepository.
/// Read-only queries dùng AsNoTracking(§4.1).
/// KHÔNG có RemoveAsync (GB-03).
/// </summary>
public sealed class FeedbackRepository : IFeedbackRepository
{
    private readonly AppDbContext _db;

    public FeedbackRepository(AppDbContext db) => _db = db;

    public async Task<ServiceFeedback> AddAsync(ServiceFeedback feedback, CancellationToken ct = default)
    {
        _db.ServiceFeedbacks.Add(feedback);
        await _db.SaveChangesAsync(ct);
        return feedback;
    }

    public async Task<IReadOnlyList<ServiceFeedback>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.ServiceFeedbacks
            .AsNoTracking()
            .Include(f => f.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(f => f.Case)
                .ThenInclude(c => c!.Doctor)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<ServiceFeedback?> GetByIdAsync(Guid feedbackId, CancellationToken ct = default)
    {
        return await _db.ServiceFeedbacks
            .AsNoTracking()
            .Include(f => f.PatientProfile)
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId, ct);
    }

    public async Task<ServiceFeedback?> GetByCaseIdAsync(Guid caseId, CancellationToken ct = default)
    {
        if (caseId == Guid.Empty) return null;

        return await _db.ServiceFeedbacks
            .AsNoTracking()
            .Include(f => f.PatientProfile)
            .FirstOrDefaultAsync(f => f.CaseId == caseId, ct);
    }

    public async Task<(IReadOnlyList<ServiceFeedback> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        short? rating,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ServiceFeedbacks
            .AsNoTracking()
            .Include(f => f.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(f => f.Case)
                .ThenInclude(c => c!.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            var isGuid = Guid.TryParse(keyword.Trim(), out var searchGuid);
            query = query.Where(f =>
                (f.CaseId.HasValue && f.CaseId.Value != Guid.Empty &&
                    ((isGuid && f.CaseId.Value == searchGuid) ||
                     f.CaseId.Value.ToString().ToLower().Contains(k))) ||
                (f.PatientProfile != null && f.PatientProfile.User != null && f.PatientProfile.User.FullName != null &&
                 f.PatientProfile.User.FullName.ToLower().Contains(k)) ||
                (f.Case != null && f.Case.Doctor != null && f.Case.Doctor.FullName != null &&
                 f.Case.Doctor.FullName.ToLower().Contains(k)) ||
                (f.Content != null && f.Content.ToLower().Contains(k)));
        }

        if (rating.HasValue && rating.Value > 0)
        {
            query = query.Where(f => f.Rating == rating.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize is < 1 or > 1000 ? 15 : pageSize;

        var items = await query
            .OrderByDescending(f => f.SubmittedAt)
            .ThenByDescending(f => f.FeedbackId)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
