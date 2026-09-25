using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-24 â€” Admin Blog Management endpoints.
/// Chá»‰ Admin má»›i truy cáº­p.
/// GB-01: Draft â†’ Published má»™t chiá»u (khÃ´ng rollback).
/// </summary>
[ApiController]
[Route("api/v1/admin/blog-posts")]
[Authorize(Roles = "ADMIN")]
[Produces("application/json")]
public sealed class AdminBlogPostsController : ControllerBase
{
        private readonly IBlogPostService _blog;
    private readonly ADSUS_BE.DAL.ExternalServices.IFileStorageService _storage;

    public AdminBlogPostsController(IBlogPostService blog, ADSUS_BE.DAL.ExternalServices.IFileStorageService storage)
    {
        _blog = blog;
        _storage = storage;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// GET /api/v1/admin/blog-posts â€” Danh sÃ¡ch táº¥t cáº£ blog (cáº£ Draft + Published), phÃ¢n trang.
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
    /// GET /api/v1/admin/blog-posts/{id} â€” Chi tiáº¿t blog (cáº£ Draft + Published).
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
                StatusCodes.Status404NotFound, "BÃ i viáº¿t khÃ´ng tá»“n táº¡i."));
        }

        return Ok(ApiResponse<AdminBlogPostDetailResponse>.Ok(result));
    }

    /// <summary>
    /// POST /api/v1/admin/blog-posts â€” Táº¡o blog post má»›i (Draft).
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
                StatusCodes.Status400BadRequest, "TiÃªu Ä‘á» khÃ´ng Ä‘Æ°á»£c trá»‘ng."));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Ná»™i dung khÃ´ng Ä‘Æ°á»£c trá»‘ng."));
        }

        var authorId = GetCurrentUserId();
        var result = await _blog.CreateAsync(request, authorId, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<AdminBlogPostDetailResponse>.Ok(result, "BÃ i viáº¿t Ä‘Ã£ Ä‘Æ°á»£c táº¡o."));
    }

    /// <summary>
    /// PUT /api/v1/admin/blog-posts/{id} â€” Cáº­p nháº­t blog post.
    /// GB-01: chá»‰ Draft má»›i cho sá»­a, Published khÃ´ng Ä‘Æ°á»£c sá»­a.
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
                StatusCodes.Status400BadRequest, "TiÃªu Ä‘á» khÃ´ng Ä‘Æ°á»£c trá»‘ng."));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Ná»™i dung khÃ´ng Ä‘Æ°á»£c trá»‘ng."));
        }

        var result = await _blog.UpdateAsync(id, request, ct);

        if (result == null)
        {
            return NotFound(ApiResponse<AdminBlogPostDetailResponse>.Fail(
                StatusCodes.Status404NotFound, "BÃ i viáº¿t khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ Ä‘Æ°á»£c xuáº¥t báº£n (khÃ´ng thá»ƒ sá»­a)."));
        }

        return Ok(ApiResponse<AdminBlogPostDetailResponse>.Ok(result, "BÃ i viáº¿t Ä‘Ã£ Ä‘Æ°á»£c cáº­p nháº­t."));
    }

    /// <summary>
    /// POST /api/v1/admin/blog-posts/{id}/publish â€” Xuáº¥t báº£n blog post.
    /// GB-01: Draft â†’ Published má»™t chiá»u (khÃ´ng rollback).
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
                StatusCodes.Status400BadRequest, "BÃ i viáº¿t khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ Ä‘Æ°á»£c xuáº¥t báº£n."));
        }

        return Ok(ApiResponse<AdminBlogPostDetailResponse>.Ok(result, "BÃ i viáº¿t Ä‘Ã£ Ä‘Æ°á»£c xuáº¥t báº£n."));
    }
    /// <summary>
    /// POST /api/v1/admin/blog-posts/upload-image â€” Upload áº£nh cho bÃ i viáº¿t
    /// </summary>
    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, "KhÃ´ng cÃ³ file nÃ o Ä‘Æ°á»£c táº£i lÃªn."));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, "Ä á»‹nh dáº¡ng áº£nh khÃ´ng há»£p lá»‡."));
        }

        var objectPath = $"blogs/{Guid.NewGuid()}{ext}";
        using var stream = file.OpenReadStream();
        
        try 
        {
            var uploadedPath = await _storage.UploadAsync(stream, objectPath, file.ContentType, "datasets", ct);
            var signedUrl = await _storage.CreateSignedUrlAsync(uploadedPath, "datasets", ct);
            return Ok(ApiResponse<object>.Ok(new { url = signedUrl ?? uploadedPath }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail(500, $"Lá»—i upload: {ex.Message}"));
        }
    }
}

