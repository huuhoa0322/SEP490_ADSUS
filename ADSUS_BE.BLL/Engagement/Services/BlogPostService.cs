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
        // Repository trả về tất cả Published, sort by PublishedAt desc
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

        return new PagedResult<BlogPostListItemResponse>
        {
            Items = paged,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
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
}
