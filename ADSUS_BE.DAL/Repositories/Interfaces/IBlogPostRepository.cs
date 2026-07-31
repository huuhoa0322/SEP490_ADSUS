using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho BlogPost. GB-03: KHÔNG có Remove/Delete.
/// GB-05: public ListPublishedAsync chỉ trả về Published.
/// </summary>
public interface IBlogPostRepository
{
    /// <summary>Danh sách tất cả blog đã xuất bản, sắp xếp PublishedAt desc.</summary>
    Task<IReadOnlyList<BlogPost>> ListPublishedAsync(CancellationToken ct = default);

    /// <summary>Danh sách tất cả blog (Admin — cả Draft + Published).</summary>
    Task<IReadOnlyList<BlogPost>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Lấy 1 blog theo ID (kèm Author navigation).</summary>
    Task<BlogPost?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
