using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.AIModelManagement.DTOs;

public class RegisterModelVersionRequest
{
    [Required]
    public string VersionCode { get; set; } = string.Empty;
    [Required]
    public string HfRepoId { get; set; } = string.Empty;
    [Required]
    public string HfFilename { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MetricsPrecision { get; set; }
    public decimal? MetricsMap50 { get; set; }
    public decimal? MetricsRecall { get; set; }
}
