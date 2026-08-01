using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Public blog post service — chỉ trả về Published posts (GB-05).
/// Repository trả về entity, service map sang DTO.
/// </summary>
public sealed class BlogPostService : IBlogPostService
{
    private readonly IBlogPostRepository _repo;

    public BlogPostService(IBlogPostRepository repo)
    {
        _repo = repo;
    }

    public async Task<PagedResult<BlogPostListItemResponse>> ListPublishedAsync(int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var posts = await _repo.ListPublishedAsync(ct);

        var totalCount = posts.Count;
        var paged = posts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new BlogPostListItemResponse
            {
                Id = p.PostId,
                Title = p.Title,
                PublishedAt = p.PublishedAt ?? p.CreatedAt,
                AuthorName = p.Author?.FullName ?? string.Empty,
            })
            .ToList();

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<BlogPostListItemResponse>(
            paged,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<BlogPostDetailResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var post = await _repo.GetByIdAsync(id, ct);

        // GB-05: chỉ Published mới trả cho bệnh nhân
        if (post == null || post.Status != BlogPostStatus.Published)
        {
            return null;
        }

        return new BlogPostDetailResponse
        {
            Id = post.PostId,
            Title = post.Title,
            Content = post.Content,
            PublishedAt = post.PublishedAt ?? post.CreatedAt,
            AuthorName = post.Author?.FullName ?? string.Empty,
        };
    }

    // ==================== Admin methods ====================

    public async Task<PagedResult<AdminBlogPostListItemResponse>> ListAllAsync(int page = 1, int pageSize = 10, BlogPostStatus? statusFilter = null, CancellationToken ct = default)
    {
        var posts = await _repo.ListAllAsync(statusFilter, ct);

        var totalCount = posts.Count;
        var paged = posts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminBlogPostListItemResponse
            {
                Id = p.PostId,
                Title = p.Title,
                Status = p.Status,
                PublishedAt = p.PublishedAt,
                CreatedAt = p.CreatedAt,
                AuthorName = p.Author?.FullName ?? string.Empty,
            })
            .ToList();

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<AdminBlogPostListItemResponse>(
            paged,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<AdminBlogPostDetailResponse?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default)
    {
        var post = await _repo.GetByIdAsync(id, ct);
        if (post == null) return null;

        return new AdminBlogPostDetailResponse
        {
            Id = post.PostId,
            Title = post.Title,
            Content = post.Content,
            Status = post.Status,
            PublishedAt = post.PublishedAt,
            CreatedAt = post.CreatedAt,
            AuthorName = post.Author?.FullName ?? string.Empty,
        };
    }

    public async Task<AdminBlogPostDetailResponse> CreateAsync(CreateBlogPostRequest request, Guid authorId, CancellationToken ct = default)
    {
        var blogPost = new BlogPost
        {
            PostId = Guid.NewGuid(),
            AuthorId = authorId,
            Title = request.Title,
            Content = request.Content,
            Status = BlogPostStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(blogPost, ct);

        return new AdminBlogPostDetailResponse
        {
            Id = blogPost.PostId,
            Title = blogPost.Title,
            Content = blogPost.Content,
            Status = blogPost.Status,
            PublishedAt = blogPost.PublishedAt,
            CreatedAt = blogPost.CreatedAt,
            AuthorName = string.Empty,
        };
    }

    public async Task<AdminBlogPostDetailResponse?> UpdateAsync(Guid id, UpdateBlogPostRequest request, CancellationToken ct = default)
    {
        var post = await _repo.GetByIdForUpdateAsync(id, ct);

        // GB-01: chỉ Draft mới cho sửa
        if (post == null || post.Status != BlogPostStatus.Draft)
        {
            return null;
        }

        post.Title = request.Title;
        post.Content = request.Content;
        post.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(post, ct);

        return new AdminBlogPostDetailResponse
        {
            Id = post.PostId,
            Title = post.Title,
            Content = post.Content,
            Status = post.Status,
            PublishedAt = post.PublishedAt,
            CreatedAt = post.CreatedAt,
            AuthorName = post.Author?.FullName ?? string.Empty,
        };
    }

    public async Task<AdminBlogPostDetailResponse?> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var post = await _repo.GetByIdForUpdateAsync(id, ct);

        // GB-01: chỉ Draft mới cho publish
        if (post == null || post.Status != BlogPostStatus.Draft)
        {
            return null;
        }

        post.Status = BlogPostStatus.Published;
        post.PublishedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(post, ct);

        return new AdminBlogPostDetailResponse
        {
            Id = post.PostId,
            Title = post.Title,
            Content = post.Content,
            Status = post.Status,
            PublishedAt = post.PublishedAt,
            CreatedAt = post.CreatedAt,
            AuthorName = post.Author?.FullName ?? string.Empty,
        };
    }
}
