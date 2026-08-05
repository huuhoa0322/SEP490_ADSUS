using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

/// <summary>
/// Request để tạo schedule slot mới (UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); Start &lt; End; range > 15 phút; không overlap.
/// DoctorId được lấy từ JWT ở Controller — Doctor chỉ tạo slot cho chính mình.
/// </summary>
public sealed class CreateScheduleSlotRequest
{
    public DateOnly VisitDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
}

/// <summary>
/// Request để update slot (tách ca, đổi giờ).
/// </summary>
public sealed class UpdateScheduleSlotRequest
{
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
}

/// <summary>
/// Response cho schedule slot (UC-15).
/// </summary>
public sealed class ScheduleSlotResponse
{
    public Guid SlotId { get; init; }
    public Guid DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public DateOnly SlotDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public SlotStatus Status { get; init; }
    public int ActiveAppointmentsCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Response chi tiết cho UC-15 AF-02 — close slot có booking.
/// Trả về số lượng appointment Booked sẽ bị ảnh hưởng để Doctor/Nurse quyết định.
/// </summary>
public sealed class CloseSlotImpactResponse
{
    public Guid SlotId { get; init; }
    public int AffectedBookingsCount { get; init; }
}