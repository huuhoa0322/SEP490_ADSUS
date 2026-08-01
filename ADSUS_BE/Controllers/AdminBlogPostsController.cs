using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-24 — Admin Blog Management endpoints.
/// Chỉ Admin mới truy cập.
/// GB-01: Draft → Published một chiều (không rollback).
/// </summary>
[ApiController]
[Route("api/v1/admin/blog-posts")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminBlogPostsController : ControllerBase
{
    private readonly IBlogPostService _blog;

    public AdminBlogPostsController(IBlogPostService blog)
    {
        _blog = blog;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// GET /api/v1/admin/blog-posts — Danh sách tất cả blog (cả Draft + Published), phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminBlogPostListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] BlogPostStatus? status = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var result = await _blog.ListAllAsync(page, pageSize, status, ct);
        return Ok(ApiResponse<PagedResult<AdminBlogPostListItemResponse>>.Ok(result));
    }

    /// <summary>
    /// GET /api/v1/admin/blog-posts/{id} — Chi tiết blog (cả Draft + Published).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _blog.GetByIdForAdminAsync(id, ct);

        if (result == null)
        {
            return NotFound(ApiResponse<AdminBlogPostDetailResponse>.Fail(
                StatusCodes.Status404NotFound, "Bài viết không tồn tại."));
        }

        return Ok(ApiResponse<AdminBlogPostDetailResponse>.Ok(result));
    }

    /// <summary>
    /// POST /api/v1/admin/blog-posts — Tạo blog post mới (Draft).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBlogPostRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Tiêu đề không được trống."));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Nội dung không được trống."));
        }

        var authorId = GetCurrentUserId();
        var result = await _blog.CreateAsync(request, authorId, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<AdminBlogPostDetailResponse>.Ok(result, "Bài viết đã được tạo."));
    }

    /// <summary>
    /// PUT /api/v1/admin/blog-posts/{id} — Cập nhật blog post.
    /// GB-01: chỉ Draft mới cho sửa, Published không được sửa.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBlogPostRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Tiêu đề không được trống."));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Nội dung không được trống."));
        }

        var result = await _blog.UpdateAsync(id, request, ct);

        if (result == null)
        {
            return NotFound(ApiResponse<AdminBlogPostDetailResponse>.Fail(
                StatusCodes.Status404NotFound, "Bài viết không tồn tại hoặc đã được xuất bản (không thể sửa)."));
        }

        return Ok(ApiResponse<AdminBlogPostDetailResponse>.Ok(result, "Bài viết đã được cập nhật."));
    }

    /// <summary>
    /// POST /api/v1/admin/blog-posts/{id}/publish — Xuất bản blog post.
    /// GB-01: Draft → Published một chiều (không rollback).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _blog.PublishAsync(id, ct);

        if (result == null)
        {
            return BadRequest(ApiResponse<AdminBlogPostDetailResponse>.Fail(
                StatusCodes.Status400BadRequest, "Bài viết không tồn tại hoặc đã được xuất bản."));
        }

        return Ok(ApiResponse<AdminBlogPostDetailResponse>.Ok(result, "Bài viết đã được xuất bản."));
    }
}
