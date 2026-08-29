using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class AiPredictionRepository : IAiPredictionRepository
{
    private readonly AppDbContext _db;

    public AiPredictionRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AiPrediction>> ListByModelVersionAsync(
        Guid modelVersionId, CancellationToken ct = default) =>
        await _db.AiPredictions
            .AsNoTracking()
            .Where(p => p.ModelVersionId == modelVersionId)
            .ToListAsync(ct);

    public async Task AddRangeAsync(IReadOnlyList<AiPrediction> predictions, CancellationToken ct = default)
    {
        _db.AiPredictions.AddRange(predictions);
        await _db.SaveChangesAsync(ct);
    }
}
