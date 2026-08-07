using System;

namespace ADSUS_BE.BLL.AIModelManagement.DTOs;

public class AiModelVersionDto
{
    public Guid ModelVersionId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MetricsPrecision { get; set; }
    public decimal? MetricsMap50 { get; set; }
    public decimal? MetricsRecall { get; set; }
    public string Status { get; set; } = string.Empty;
    public string HfRepoId { get; set; } = string.Empty;
    public string HfFilename { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public Guid RegisteredBy { get; set; }

    public int LiveTp { get; set; }
    public int LiveFp { get; set; }
    public int LiveFn { get; set; }
    public decimal? LiveMap50 { get; set; }
    public DateTime? LastEvaluatedAt { get; set; }
    
    public decimal? LivePrecision => (LiveTp + LiveFp) > 0 ? (decimal)LiveTp / (LiveTp + LiveFp) : null;
    public decimal? LiveRecall => (LiveTp + LiveFn) > 0 ? (decimal)LiveTp / (LiveTp + LiveFn) : null;
}
