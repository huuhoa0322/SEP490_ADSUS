using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Phiên bản mô hình AI (đăng ký / kích hoạt / rollback / theo dõi — FT-23/24/25). Partial unique index bảo đảm chỉ 1 phiên bản ACTIVE. Rollback = ACTIVE → INACTIVE và kích hoạt bản khác.
/// </summary>
public partial class AiModelVersion
{
    public Guid ModelVersionId { get; set; }

    public string VersionCode { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Chỉ số đo offline khi đăng ký, đơn vị %. Ngưỡng KPI nghiên cứu: &gt; 90%.
    /// </summary>
    public decimal? EvalSensitivity { get; set; }

    /// <summary>
    /// Đơn vị %. Ngưỡng KPI: &gt; 85%.
    /// </summary>
    public decimal? EvalAccuracy { get; set; }

    /// <summary>
    /// Thang 0–1. Ngưỡng KPI: &gt; 0.90.
    /// </summary>
    public decimal? EvalAuc { get; set; }

    public Guid RegisteredBy { get; set; }

    public DateTime RegisteredAt { get; set; }

    public virtual ICollection<AiResult> AiResults { get; set; } = new List<AiResult>();

    public virtual User RegisteredByNavigation { get; set; } = null!;
}
