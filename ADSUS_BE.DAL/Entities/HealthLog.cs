using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Nhật ký FT-35. created_at là mốc JOB-03 kiểm tra chu kỳ nhắc 6 giờ (FT-40); widget màn hình chính (FT-41) đọc các dòng gần nhất.
/// </summary>
public partial class HealthLog
{
    public Guid HealthLogId { get; set; }

    public Guid PatientProfileId { get; set; }

    public DateOnly LogDate { get; set; }

    public HealthLogType LogType { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual PatientProfile PatientProfile { get; set; } = null!;
}
