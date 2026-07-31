namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Generic pagination wrapper — dùng chung cho tất cả list endpoint.
/// </summary>
/// <typeparam name="T">Type của item trong danh sách.</typeparam>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; } = 0;
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Response cho blog post list (PUBLIC — chỉ Published).
/// </summary>
public sealed class BlogPostListItemResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public string AuthorName { get; init; } = string.Empty;
}

/// <summary>
/// Response cho blog post detail (PUBLIC — chỉ Published).
/// </summary>
public sealed class BlogPostDetailResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public string AuthorName { get; init; } = string.Empty;
}
