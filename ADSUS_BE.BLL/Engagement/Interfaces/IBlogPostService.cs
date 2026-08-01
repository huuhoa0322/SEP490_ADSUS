using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.Engagement.Interfaces;

/// <summary>
/// Service cho blog posts (PUBLIC endpoints). GB-05: chỉ Published mới trả về cho bệnh nhân.
/// </summary>
public interface IBlogPostService
{
    /// <summary>Danh sách blog đã xuất bản, phân trang.</summary>
    Task<PagedResult<BlogPostListItemResponse>> ListPublishedAsync(int page = 1, int pageSize = 10, CancellationToken ct = default);

    /// <summary>Chi tiết blog đã xuất bản. Trả null nếu Draft hoặc không tồn tại (GB-05).</summary>
    Task<BlogPostDetailResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // ==================== Admin methods ====================

    /// <summary>Admin: danh sách tất cả blog (cả Draft + Published), phân trang.</summary>
    Task<PagedResult<AdminBlogPostListItemResponse>> ListAllAsync(int page = 1, int pageSize = 10, BlogPostStatus? statusFilter = null, CancellationToken ct = default);

    /// <summary>Admin: chi tiết blog (cả Draft + Published).</summary>
    Task<AdminBlogPostDetailResponse?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default);

    /// <summary>Admin: tạo blog post mới (Draft).</summary>
    Task<AdminBlogPostDetailResponse> CreateAsync(CreateBlogPostRequest request, Guid authorId, CancellationToken ct = default);

    /// <summary>Admin: cập nhật blog post (chỉ Draft). GB-01: không cho sửa khi Published.</summary>
    Task<AdminBlogPostDetailResponse?> UpdateAsync(Guid id, UpdateBlogPostRequest request, CancellationToken ct = default);

    /// <summary>Admin: xuất bản blog post. GB-01: Draft → Published (một chiều).</summary>
    Task<AdminBlogPostDetailResponse?> PublishAsync(Guid id, CancellationToken ct = default);
}
