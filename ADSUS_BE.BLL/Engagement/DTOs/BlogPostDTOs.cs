namespace ADSUS_BE.BLL.Engagement.DTOs;

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
