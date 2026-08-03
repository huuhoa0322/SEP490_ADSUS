namespace ADSUS_BE.BLL.AIModelManagement.DTOs;

public class RegisterModelVersionRequest
{
    public string VersionCode { get; set; } = string.Empty;
    public string HfRepoId { get; set; } = string.Empty;
    public string HfFilename { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MetricsPrecision { get; set; }
    public decimal? MetricsMap50 { get; set; }
    public decimal? MetricsRecall { get; set; }
}
