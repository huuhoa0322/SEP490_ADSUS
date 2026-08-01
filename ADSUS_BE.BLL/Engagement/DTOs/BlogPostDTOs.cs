using ADSUS_BE.DAL.Entities;

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

// ==================== Admin DTOs ====================

/// <summary>
/// Response cho admin blog post list (cả Draft + Published).
/// </summary>
public sealed class AdminBlogPostListItemResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public BlogPostStatus Status { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string AuthorName { get; init; } = string.Empty;
}

/// <summary>
/// Response cho admin blog post detail (cả Draft + Published).
/// </summary>
public sealed class AdminBlogPostDetailResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public BlogPostStatus Status { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string AuthorName { get; init; } = string.Empty;
}

/// <summary>
/// Request để tạo blog post mới (Admin).
/// </summary>
public sealed class CreateBlogPostRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Request để cập nhật blog post (Admin — chỉ Draft).
/// </summary>
public sealed class UpdateBlogPostRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
