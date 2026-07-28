using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Kết quả 1 lần chạy AI (FT-18). Vòng đời PENDING_REVIEW → CONFIRMED/REJECTED là hiện thân của quy tắc &quot;AI hỗ trợ, KHÔNG thay thế bác sĩ&quot;: bệnh nhân chỉ thấy kết quả CONFIRMED (§3.2 Restricted ²). ck_ai_results_review_state khóa cứng tính nhất quán của bước duyệt.
/// </summary>
public partial class AiResult
{
    public Guid AiResultId { get; set; }

    public Guid CaseId { get; set; }

    /// <summary>
    /// Truy vết bắt buộc của AI y tế: kết quả này do phiên bản mô hình nào sinh ra — không mất dấu khi rollback (FT-24).
    /// </summary>
    public Guid ModelVersionId { get; set; }

    public Guid? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string? DoctorNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AiFinding> AiFindings { get; set; } = new List<AiFinding>();

    public virtual Case Case { get; set; } = null!;

    public virtual User? ConfirmedByNavigation { get; set; }

    public virtual AiModelVersion ModelVersion { get; set; } = null!;
}
