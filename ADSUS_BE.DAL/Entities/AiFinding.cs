using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// 1 lần chạy AI phát hiện 0..n vùng bất thường (FT-19 &quot;regions&quot; số nhiều) — mỗi vùng có mask/phân loại/độ tin riêng nên bắt buộc tách bảng. CASCADE theo ai_results vì là con-thành-phần.
/// </summary>
public partial class AiFinding
{
    public Guid FindingId { get; set; }

    public Guid AiResultId { get; set; }

    public Guid ImageId { get; set; }

    public string? MaskRef { get; set; }

    public string? LesionType { get; set; }

    public decimal? Confidence { get; set; }

    /// <summary>
    /// Kích thước vùng bất thường — nguyên liệu cho theo dõi tiến triển (FT-22): so size qua các lượt khám.
    /// </summary>
    public decimal? SizeMm { get; set; }

    public virtual AiResult AiResult { get; set; } = null!;

    public virtual UltrasoundImage Image { get; set; } = null!;
}
