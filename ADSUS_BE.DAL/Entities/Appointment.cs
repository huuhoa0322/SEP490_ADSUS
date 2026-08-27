using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Lịch khám đã đặt (UC-13/14). Đổi lịch = CANCELLED dòng cũ + tạo dòng mới (giữ vết). Job JOB-02 đọc bảng này để nhắc lịch qua push. Chỉ 2 trạng thái BOOKED/CANCELLED — không có COMPLETED: lịch &quot;đã qua&quot; suy ra ở tầng ứng dụng bằng cách so schedule_slots.end_time với NOW(), không lưu trạng thái riêng (tránh job quét/cập nhật hàng loạt).
/// </summary>
public partial class Appointment
{
    public Guid AppointmentId { get; set; }

    public Guid SlotId { get; set; }

    public Guid PatientProfileId { get; set; }

    public string? Reason { get; set; }

    public string? CancelledReason { get; set; }

    /// <summary>
    /// Mốc đã đẩy sự kiện sang Calendar thiết bị (FT-34, one-way sync) — sự kiện nằm NGOÀI hệ thống, chỉ giữ timestamp.
    /// </summary>
    public DateTime? CalendarSyncedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CaseId { get; set; }

    public virtual Case? Case { get; set; }

    public virtual PatientProfile PatientProfile { get; set; } = null!;

    public virtual ScheduleSlot Slot { get; set; } = null!;
}
