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

    public async Task<IReadOnlyList<BlogPost>> ListAllAsync(BlogPostStatus? statusFilter = null, CancellationToken ct = default)
    {
        IQueryable<BlogPost> query = _db.BlogPosts.AsNoTracking().Include(b => b.Author);

        if (statusFilter.HasValue)
        {
            query = query.Where(b => b.Status == statusFilter.Value);
        }

        return await query
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

    public async Task<BlogPost?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        // Tracking enabled for update
        return await _db.BlogPosts
            .Include(b => b.Author)
            .FirstOrDefaultAsync(b => b.PostId == id, ct);
    }

    public async Task<BlogPost> AddAsync(BlogPost blogPost, CancellationToken ct = default)
    {
        _db.BlogPosts.Add(blogPost);
        await _db.SaveChangesAsync(ct);
        return blogPost;
    }

    public async Task UpdateAsync(BlogPost blogPost, CancellationToken ct = default)
    {
        _db.BlogPosts.Update(blogPost);
        await _db.SaveChangesAsync(ct);
    }
}
