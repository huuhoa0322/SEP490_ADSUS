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
    /// Chỉ số đo offline khi đăng ký, đơn vị %. Chỉ để Admin tham khảo khi quyết định
    /// Activate/Rollback — không còn là ngưỡng KPI bắt buộc (ngưỡng cứng Sensitivity/Accuracy/AUC
    /// cũ đã bị bỏ 08/08/2026, xem Report3 EN IV.2 Quality Attributes).
    /// </summary>
    public decimal? MetricsPrecision { get; set; }

    /// <summary>
    /// Đơn vị %. Chỉ để Admin tham khảo — không còn là ngưỡng KPI bắt buộc (đã bỏ 08/08/2026).
    /// </summary>
    public decimal? MetricsMap50 { get; set; }

    /// <summary>
    /// Thang 0–1. Chỉ để Admin tham khảo — không còn là ngưỡng KPI bắt buộc (đã bỏ 08/08/2026).
    /// </summary>
    public decimal? MetricsRecall { get; set; }

    public Guid RegisteredBy { get; set; }

    public DateTime RegisteredAt { get; set; }

    public string HfRepoId { get; set; } = null!;

    public string HfFilename { get; set; } = null!;

    public int LiveTp { get; set; }

    public int LiveFp { get; set; }

    public int LiveFn { get; set; }

    public decimal? LiveMap50 { get; set; }

    public DateTime? LastEvaluatedAt { get; set; }

    public virtual ICollection<AiPrediction> AiPredictions { get; set; } = new List<AiPrediction>();

    public virtual User RegisteredByNavigation { get; set; } = null!;
}
