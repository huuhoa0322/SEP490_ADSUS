using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AIModelManagement.Mappers;

public static class AiModelVersionMapper
{
    public static AiModelVersionDto ToDto(AiModelVersion v) => new()
    {
        ModelVersionId = v.ModelVersionId,
        VersionCode = v.VersionCode,
        Description = v.Description,
        MetricsPrecision = v.MetricsPrecision,
        MetricsMap50 = v.MetricsMap50,
        MetricsRecall = v.MetricsRecall,
        Status = v.Status.ToString(),
        HfRepoId = v.HfRepoId,
        HfFilename = v.HfFilename,
        RegisteredAt = v.RegisteredAt,
        RegisteredBy = v.RegisteredBy,
        LiveTp = v.LiveTp,
        LiveFp = v.LiveFp,
        LiveFn = v.LiveFn,
        LiveMap50 = v.LiveMap50,
        LastEvaluatedAt = v.LastEvaluatedAt
    };

    /// <summary>Doctor-facing, chỉ code/status (UC-20) — xem <see cref="ActiveAiModelVersionDto"/>.</summary>
    public static ActiveAiModelVersionDto ToActiveDto(AiModelVersion v) => new()
    {
        VersionCode = v.VersionCode,
        Status = v.Status.ToString()
    };
}
