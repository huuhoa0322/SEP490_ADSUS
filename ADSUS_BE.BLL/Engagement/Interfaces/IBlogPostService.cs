using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;

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
}
