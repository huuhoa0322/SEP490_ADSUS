using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IAiModelVersionRepository
{
    Task<IReadOnlyList<AiModelVersion>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(List<AiModelVersion> Items, int TotalItems)> SearchAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AiModelVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default);
    Task<AiModelVersion?> GetActiveVersionAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AiModelVersion modelVersion, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
