using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// BlogPost.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// GB-01: Draft → Published một chiều (không rollback).
/// </summary>
public partial class BlogPost
{
    [Column("status")]
    public BlogPostStatus Status { get; set; } = BlogPostStatus.Draft;
}
