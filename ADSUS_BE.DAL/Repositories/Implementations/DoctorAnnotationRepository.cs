using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class DoctorAnnotationRepository : IDoctorAnnotationRepository
{
    private readonly AppDbContext _db;

    public DoctorAnnotationRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DoctorAnnotation>> ListByImageForUpdateAsync(Guid imageId, CancellationToken ct = default) =>
        await _db.DoctorAnnotations.Where(a => a.ImageId == imageId).ToListAsync(ct);

    public void RemoveRange(IEnumerable<DoctorAnnotation> annotations) => _db.DoctorAnnotations.RemoveRange(annotations);

    public async Task<IReadOnlyList<DoctorAnnotation>> ListByImageIdsAsync(
        IReadOnlyList<Guid> imageIds, CancellationToken ct = default) =>
        await _db.DoctorAnnotations
            .AsNoTracking()
            .Where(a => imageIds.Contains(a.ImageId))
            .ToListAsync(ct);

    public async Task AddRangeAsync(IReadOnlyList<DoctorAnnotation> annotations, CancellationToken ct = default)
    {
        _db.DoctorAnnotations.AddRange(annotations);
        await _db.SaveChangesAsync(ct);
    }
}
