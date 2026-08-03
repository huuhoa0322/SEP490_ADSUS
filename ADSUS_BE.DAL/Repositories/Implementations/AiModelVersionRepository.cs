using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class AiModelVersionRepository : IAiModelVersionRepository
{
    private readonly AppDbContext _context;

    public AiModelVersionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AiModelVersion>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AiModelVersions
            .OrderByDescending(x => x.RegisteredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<AiModelVersion> Items, int TotalItems)> SearchAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.AiModelVersions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            query = query.Where(x => x.VersionCode.ToLower().Contains(lowerKeyword) || x.HfFilename.ToLower().Contains(lowerKeyword));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.RegisteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    public async Task<AiModelVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AiModelVersions
            .FirstOrDefaultAsync(x => x.ModelVersionId == id, cancellationToken);
    }

    public async Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default)
    {
        return await _context.AiModelVersions
            .AnyAsync(x => x.VersionCode.ToLower() == versionCode.ToLower(), cancellationToken);
    }

    public async Task<AiModelVersion?> GetActiveVersionAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AiModelVersions
            .FirstOrDefaultAsync(x => x.Status == ModelVersionStatus.Active, cancellationToken);
    }

    public async Task AddAsync(AiModelVersion modelVersion, CancellationToken cancellationToken = default)
    {
        await _context.AiModelVersions.AddAsync(modelVersion, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            await _context.Database.CurrentTransaction.RollbackAsync(cancellationToken);
        }
    }
}
