using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IBlogPostRepository.
/// Read-only queries dùng AsNoTracking(§4.1).
/// KHÔNG có RemoveAsync (GB-03).
/// </summary>
public sealed class BlogPostRepository : IBlogPostRepository
{
    private readonly AppDbContext _db;

    public BlogPostRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BlogPost>> ListPublishedAsync(CancellationToken ct = default)
    {
        return await _db.BlogPosts
            .AsNoTracking()
            .Include(b => b.Author)
            .Where(b => b.Status == BlogPostStatus.Published)
            .OrderByDescending(b => b.PublishedAt)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BlogPost>> ListAllAsync(CancellationToken ct = default)
    {
        return await _db.BlogPosts
            .AsNoTracking()
            .Include(b => b.Author)
            .OrderByDescending(b => b.Status == BlogPostStatus.Published ? b.PublishedAt : b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<BlogPost?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.BlogPosts
            .AsNoTracking()
            .Include(b => b.Author)
            .FirstOrDefaultAsync(b => b.PostId == id, ct);
    }
}
