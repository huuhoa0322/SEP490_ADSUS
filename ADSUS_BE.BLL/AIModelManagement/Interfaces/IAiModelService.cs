using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.Common;

namespace ADSUS_BE.BLL.AIModelManagement.Interfaces;

public interface IAiModelService
{
    Task<PagedResult<AiModelVersionDto>> SearchVersionsAsync(string? keyword, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    
    Task<AiModelVersionDto> GetVersionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ActiveAiModelVersionDto?> GetActiveVersionAsync(CancellationToken cancellationToken = default);

    Task<AiModelVersionDto> RegisterVersionAsync(
        RegisterModelVersionRequest request, 
        Guid adminId, 
        CancellationToken cancellationToken = default);

    Task UpdateVersionAsync(
        Guid id,
        UpdateModelVersionRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task ActivateVersionAsync(Guid id, Guid adminId, CancellationToken cancellationToken = default);
}
