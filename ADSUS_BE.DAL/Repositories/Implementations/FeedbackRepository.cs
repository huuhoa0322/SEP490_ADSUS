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
        return await _db.ServiceFeedbacks
            .AsNoTracking()
            .Include(f => f.PatientProfile)
            .FirstOrDefaultAsync(f => f.CaseId == caseId, ct);
    }
}
