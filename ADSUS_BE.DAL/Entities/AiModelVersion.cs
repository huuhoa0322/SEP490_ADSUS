using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Phiên bản mô hình AI (thêm mới / kích hoạt / rollback / theo dõi — FT-23/24/25). Chỉ 2 trạng thái Active/Inactive — phiên bản mới thêm mặc định Inactive cho tới khi Admin kích hoạt. Partial unique index bảo đảm chỉ 1 phiên bản ACTIVE. Rollback = ACTIVE → INACTIVE và kích hoạt bản khác.
/// </summary>
public partial class AiModelVersion
{
    public Guid ModelVersionId { get; set; }

    public string VersionCode { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Chỉ số đo offline khi đăng ký, đơn vị %. Ngưỡng KPI nghiên cứu: &gt; 90%.
    /// </summary>
    public decimal? MetricsPrecision { get; set; }

    /// <summary>
    /// Đơn vị %. Ngưỡng KPI: &gt; 85%.
    /// </summary>
    public decimal? MetricsMap50 { get; set; }

    /// <summary>
    /// Thang 0–1. Ngưỡng KPI: &gt; 0.90.
    /// </summary>
    public decimal? MetricsRecall { get; set; }

    public Guid RegisteredBy { get; set; }

    public DateTime RegisteredAt { get; set; }

    public string HfRepoId { get; set; } = null!;

    public string HfFilename { get; set; } = null!;

    public virtual ICollection<AiPrediction> AiPredictions { get; set; } = new List<AiPrediction>();

    public virtual User RegisteredByNavigation { get; set; } = null!;
}
