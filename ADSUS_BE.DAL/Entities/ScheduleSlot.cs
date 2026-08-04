using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Quỹ giờ khám bác sĩ công bố (UC-15) — không giới hạn số Appointment/slot, vòng đời chỉ Open → Closed (quyết định UCS 3.1, 23/07/2026).
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
