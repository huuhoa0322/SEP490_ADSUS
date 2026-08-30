using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.AIModelManagement.Interfaces;
using ADSUS_BE.BLL.AIModelManagement.Mappers;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.AIModelManagement.Services;

public class AiModelService : IAiModelService
{
    private readonly IAiModelVersionRepository _aiModelVersionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiBackendSettings _aiBackendSettings;
    private readonly ILogger<AiModelService> _logger;

    public AiModelService(
        IAiModelVersionRepository aiModelVersionRepository,
        IAuditLogRepository auditLogRepository,
        IHttpClientFactory httpClientFactory,
        IOptions<AiBackendSettings> aiBackendSettings,
        ILogger<AiModelService> logger)
    {
        _aiModelVersionRepository = aiModelVersionRepository;
        _auditLogRepository = auditLogRepository;
        _httpClientFactory = httpClientFactory;
        _aiBackendSettings = aiBackendSettings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<AiModelVersionDto>> SearchVersionsAsync(string? keyword, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var (items, totalItems) = await _aiModelVersionRepository.SearchAsync(keyword, page, pageSize, cancellationToken);

        var dtoList = items.Select(AiModelVersionMapper.ToDto).ToList();

        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        return new PagedResult<AiModelVersionDto>(dtoList, page, pageSize, totalItems, totalPages);
    }

    public async Task<AiModelVersionDto> GetVersionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var v = await _aiModelVersionRepository.GetByIdAsync(id, cancellationToken);
        if (v == null)
        {
            throw new ResourceNotFoundException("Không tìm thấy phiên bản AI này.");
        }

        return AiModelVersionMapper.ToDto(v);
    }

    public async Task<ActiveAiModelVersionDto?> GetActiveVersionAsync(CancellationToken cancellationToken = default)
    {
        var v = await _aiModelVersionRepository.GetActiveVersionReadOnlyAsync(cancellationToken);
        return v == null ? null : AiModelVersionMapper.ToActiveDto(v);
    }

    public async Task<AiModelVersionDto> RegisterVersionAsync(
        RegisterModelVersionRequest request, 
        Guid adminId, 
        CancellationToken cancellationToken = default)
    {
        // BR-01: Unique version code
        if (await _aiModelVersionRepository.VersionCodeExistsAsync(request.VersionCode, cancellationToken))
        {
            throw new BusinessException($"Mã phiên bản '{request.VersionCode}' đã tồn tại.");
        }

        var newVersion = new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(),
            VersionCode = request.VersionCode,
            HfRepoId = request.HfRepoId,
            HfFilename = request.HfFilename,
            Description = request.Description,
            MetricsPrecision = request.MetricsPrecision,
            MetricsMap50 = request.MetricsMap50,
            MetricsRecall = request.MetricsRecall,
            Status = ModelVersionStatus.Inactive,
            RegisteredBy = adminId,
            RegisteredAt = DateTime.UtcNow
        };

        await _aiModelVersionRepository.AddAsync(newVersion, cancellationToken);
        
        // BR-05: Audit Log
        await _auditLogRepository.AddAsync(new AuditLog
        {
            LogId = Guid.NewGuid(),
            ActorId = adminId,
            Action = "REGISTER_AI_MODEL",
            Detail = $"Registered AI model version {newVersion.VersionCode}",
            PerformedAt = DateTime.UtcNow
        }, cancellationToken);

        await _aiModelVersionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AI model version {ModelVersionId} ({VersionCode}) registered by admin {AdminId}",
            newVersion.ModelVersionId, newVersion.VersionCode, adminId);

        return AiModelVersionMapper.ToDto(newVersion);
    }

    public async Task UpdateVersionAsync(Guid id, UpdateModelVersionRequest request, Guid adminId, CancellationToken cancellationToken = default)
    {
        var version = await _aiModelVersionRepository.GetByIdAsync(id, cancellationToken);
        if (version == null)
        {
            throw new ResourceNotFoundException("Không tìm thấy phiên bản AI này.");
        }

        if (version.Status == ModelVersionStatus.Active)
        {
            throw new BusinessException("Không thể sửa đổi thông tin của phiên bản đang ACTIVE. Vui lòng tạm ngưng (activate phiên bản khác) trước khi sửa.");
        }

        version.Description = request.Description;
        version.MetricsPrecision = request.MetricsPrecision;
        version.MetricsMap50 = request.MetricsMap50;
        version.MetricsRecall = request.MetricsRecall;
        version.HfRepoId = request.HfRepoId;
        version.HfFilename = request.HfFilename;
        
        await _auditLogRepository.AddAsync(new AuditLog
        {
            LogId = Guid.NewGuid(),
            PerformedAt = DateTime.UtcNow,
            ActorId = adminId,
            Action = "UPDATE_AI_MODEL",
            Detail = $"Đã cập nhật thông tin phiên bản AI: {version.VersionCode} ({version.ModelVersionId})"
        }, cancellationToken);

        await _aiModelVersionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AI model version {ModelVersionId} ({VersionCode}) updated by admin {AdminId}",
            version.ModelVersionId, version.VersionCode, adminId);
    }

    public async Task ActivateVersionAsync(Guid id, Guid adminId, CancellationToken cancellationToken = default)
    {
        var targetVersion = await _aiModelVersionRepository.GetByIdAsync(id, cancellationToken);
        if (targetVersion == null)
        {
            throw new ResourceNotFoundException("Không tìm thấy phiên bản AI này.");
        }

        if (targetVersion.Status == ModelVersionStatus.Active)
        {
            throw new BusinessException("Phiên bản này đang được kích hoạt.");
        }

        // BR-02: Single-Active constraint (chỉ 1 bản Active)
        var currentActive = await _aiModelVersionRepository.GetActiveVersionAsync(cancellationToken);
        
        await _aiModelVersionRepository.BeginTransactionAsync(cancellationToken);

        try
        {
            // Cập nhật Database
            if (currentActive != null)
            {
                currentActive.Status = ModelVersionStatus.Inactive;
                // Force push to database to avoid unique constraint violation with the new active model
                await _aiModelVersionRepository.SaveChangesAsync(cancellationToken);
            }
            
            targetVersion.Status = ModelVersionStatus.Active;

            // Gọi Webhook sang Python để nạp model (nếu lỗi sẽ ném exception, tự rollback request)
            var httpClient = _httpClientFactory.CreateClient("AiBackend");
            
            if (!string.IsNullOrEmpty(_aiBackendSettings.Token))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _aiBackendSettings.Token);
            }

            var payload = new { repo_id = targetVersion.HfRepoId, filename = targetVersion.HfFilename };
            
            var endpoint = _aiBackendSettings.WebhookUrl.TrimEnd('/') + "/api/reload-model";
            
            var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogWarning(
                    "AI backend refused to reload model {ModelVersionId} ({VersionCode}): {StatusCode} {ErrorBody}",
                    targetVersion.ModelVersionId, targetVersion.VersionCode, response.StatusCode, errorBody);

                if (errorBody.Contains("Repository Not Found", StringComparison.OrdinalIgnoreCase) ||
                    errorBody.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                    errorBody.Contains("Entry not found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessException("Không tìm thấy mô hình trên HuggingFace. Vui lòng kiểm tra lại Repo ID và Filename.");
                }

                if (errorBody.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                    errorBody.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessException("Lỗi xác thực với HuggingFace. Vui lòng kiểm tra lại cấu hình Token (HUGGING_FACE_HUB_TOKEN) trên Python Backend.");
                }

                throw new BusinessException("Quá trình kích hoạt thất bại. Hệ thống Python Backend không thể nạp mô hình lúc này.");
            }

            // BR-05: Audit Log
            string actionType = currentActive != null && targetVersion.RegisteredAt < currentActive.RegisteredAt 
                ? "ROLLBACK_AI_MODEL" 
                : "ACTIVATE_AI_MODEL";

            await _auditLogRepository.AddAsync(new AuditLog
            {
                LogId = Guid.NewGuid(),
                ActorId = adminId,
                Action = actionType,
                Detail = $"{actionType} version {targetVersion.VersionCode}. Prev active: {(currentActive != null ? currentActive.VersionCode : "None")}",
                PerformedAt = DateTime.UtcNow
            }, cancellationToken);

            // Commit DB sau khi Python backend đã nạp thành công
            await _aiModelVersionRepository.SaveChangesAsync(cancellationToken);
            await _aiModelVersionRepository.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "{ActionType} version {VersionCode} by admin {AdminId}. Prev active: {PrevVersionCode}",
                actionType, targetVersion.VersionCode, adminId, currentActive?.VersionCode ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to activate AI model version {ModelVersionId} ({VersionCode}) — rolling back",
                targetVersion.ModelVersionId, targetVersion.VersionCode);
            await _aiModelVersionRepository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
