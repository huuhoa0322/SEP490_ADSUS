namespace ADSUS_BE.BLL.AIModelManagement.DTOs;

public class UpdateModelVersionRequest
{
    public string? Description { get; set; }
    
    public decimal? MetricsPrecision { get; set; }
    public decimal? MetricsMap50 { get; set; }
    public decimal? MetricsRecall { get; set; }
    
    public string HfRepoId { get; set; } = null!;
    public string HfFilename { get; set; } = null!;
}
