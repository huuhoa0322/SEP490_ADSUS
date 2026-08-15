using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Giờ nhắc uống thuốc do bệnh nhân tự chỉnh theo từng khung (MORNING/NOON/EVENING), áp dụng cho MỌI thuốc — không gắn với 1 đơn cụ thể, chỉnh 1 lần dùng mãi về sau. Mặc định hệ thống khi bệnh nhân chưa có dòng tùy chỉnh: Sáng 07:00 / Trưa 12:00 / Tối 20:00 (áp ở tầng ứng dụng, không lưu dòng mặc định vào bảng này). JOB-01 tra bảng này khi sinh scheduled_time cho medication_intake_logs mới.
/// </summary>
public partial class PatientReminderPreference
{
    public Guid PreferenceId { get; set; }

    public Guid PatientProfileId { get; set; }

    public bool? NotifEnabled { get; set; }

    public TimeOnly? MorningTime { get; set; }

    public TimeOnly? MiddayTime { get; set; }

    public TimeOnly? EveningTime { get; set; }

    public virtual PatientProfile PatientProfile { get; set; } = null!;
}
