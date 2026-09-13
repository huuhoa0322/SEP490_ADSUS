using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-23 — Blog Sức khỏe endpoints.
/// GB-05: bệnh nhân chỉ thấy Published.
/// 2026-09-12: mở public cho Guest (chưa đăng nhập) + Patient + tất cả role (Admin dùng
/// /admin/blog-posts riêng). Quyết định chốt với user trong session landing-page.
/// Xem project-state/decisions.md để biết lý do override GB-09 cũ.
/// </summary>
[ApiController]
[Route("api/v1/blog-posts")]
[Produces("application/json")]
public sealed class BlogPostsController : ControllerBase
{
    private readonly IBlogPostService _blog;

    public BlogPostsController(IBlogPostService blog)
    {
        _blog = blog;
    }

    /// <summary>
    /// GET /api/v1/blog-posts — Danh sách bài viết đã xuất bản, phân trang.
    /// Public: Guest + Patient + mọi role đều xem được.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BlogPostListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50; // cap để tránh query quá nặng

        var result = await _blog.ListPublishedAsync(page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<BlogPostListItemResponse>>.Ok(result));
    }

    /// <summary>
    /// GET /api/v1/blog-posts/{id} — Chi tiết bài viết.
    /// GB-05: trả 404 nếu Draft hoặc không tồn tại (không trả 403 để không leak status).
    /// Public: Guest + Patient + mọi role đều xem được.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _blog.GetByIdAsync(id, ct);

        if (result == null)
        {
            return NotFound(ApiResponse<BlogPostDetailResponse>.Fail(
                StatusCodes.Status404NotFound, "Bài viết không tồn tại hoặc chưa được xuất bản."));
        }

        return Ok(ApiResponse<BlogPostDetailResponse>.Ok(result));
    }
}
