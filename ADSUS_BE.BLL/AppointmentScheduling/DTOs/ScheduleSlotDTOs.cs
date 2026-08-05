using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

/// <summary>
/// Request để tạo schedule slot mới (UC-15).
/// Validation: DoctorId tồn tại, VisitDate không trong quá khứ, Start < End, range > 15 phút, không overlap.
/// </summary>
public sealed class CreateScheduleSlotRequest
{
    public Guid DoctorId { get; init; }
    public DateOnly VisitDate { get; init; }
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