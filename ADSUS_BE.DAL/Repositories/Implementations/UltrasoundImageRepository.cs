using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class UltrasoundImageRepository : IUltrasoundImageRepository
{
    private readonly AppDbContext _db;

    public UltrasoundImageRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UltrasoundImage>> ListByCaseAsync(
        Guid caseId,
        CancellationToken ct = default) =>
        await _db.UltrasoundImages
            .AsNoTracking()
            .Where(i => i.CaseId == caseId)
            .OrderBy(i => i.UploadedAt)
            .ToListAsync(ct);

    public async Task AddRangeAsync(IReadOnlyList<UltrasoundImage> images, CancellationToken ct = default)
    {
        _db.UltrasoundImages.AddRange(images);
        await _db.SaveChangesAsync(ct);
    }
}
