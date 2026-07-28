using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Mỗi liều thuốc = 1 dòng. Tuân thủ điều trị (FT-27) = tỉ lệ TAKEN trên tổng liều. PENDING là bổ sung vật lý: job sinh dòng trước, bệnh nhân xác nhận sau (FT-29). Không có trạng thái Missed/Skipped — JOB-01 nhắc lặp lại định kỳ khi còn PENDING, chỉ dừng khi bệnh nhân xác nhận TAKEN.
/// </summary>
public partial class MedicationIntakeLog
{
    public Guid IntakeId { get; set; }

    public Guid PrescriptionItemId { get; set; }

    public DateTime ScheduledTime { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public virtual PrescriptionItem PrescriptionItem { get; set; } = null!;
}
