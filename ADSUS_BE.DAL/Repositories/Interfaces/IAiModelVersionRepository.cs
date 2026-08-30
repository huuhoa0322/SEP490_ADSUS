using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IAiModelVersionRepository
{
    Task<(List<AiModelVersion> Items, int TotalItems)> SearchAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AiModelVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default);
    /// <summary>
    /// Dùng khi CÓ sửa bản ghi trả về (ví dụ AiModelService.ActivateAsync đổi Status rồi
    /// SaveChangesAsync). Chỗ nào chỉ đọc, dùng <see cref="GetActiveVersionReadOnlyAsync"/>.
    /// </summary>
    Task<AiModelVersion?> GetActiveVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc phiên bản AI đang Active CHỈ ĐỂ HIỂN THỊ — AsNoTracking thật sự (P11 review
    /// Feature 3, 28/08/2026). Dùng cho các luồng không bao giờ sửa-rồi-lưu lại chính entity
    /// vừa đọc (ví dụ DashboardService, CaseDiagnosisService).
    /// </summary>
    Task<AiModelVersion?> GetActiveVersionReadOnlyAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AiModelVersion modelVersion, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
