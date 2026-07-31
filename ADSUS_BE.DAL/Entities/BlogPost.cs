using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Blog sức khỏe (UC-23/24). Bệnh nhân chỉ thấy PUBLISHED (§3.2: Patient chỉ có quyền View).
/// GB-01: Draft → Published một chiều (không rollback).
/// </summary>
public partial class BlogPost
{
    public Guid PostId { get; set; }

    public Guid AuthorId { get; set; }

    /// <summary>Trạng thái: Draft hoặc Published. Derived từ DB enum blog_status.</summary>
    public BlogPostStatus Status { get; set; } = BlogPostStatus.Draft;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Author { get; set; } = null!;
}
