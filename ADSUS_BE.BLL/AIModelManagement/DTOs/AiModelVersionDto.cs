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
}
