using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Quỹ giờ khám bác sĩ công bố (UC-15) — bệnh nhân đặt lịch bằng cách chọn slot OPEN (UC-13). EXCLUDE constraint chặn khung giờ chồng lấn của cùng bác sĩ ngay tại DB. Đơn giản hóa v2: mỗi khung giờ mặc định 1 bệnh nhân (không có capacity) — status chuyển FULL ngay khi có 1 booking BOOKED.
/// </summary>
public partial class ScheduleSlot
{
    public Guid SlotId { get; set; }

    public Guid DoctorId { get; set; }

    public DateOnly SlotDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual User Doctor { get; set; } = null!;
}
