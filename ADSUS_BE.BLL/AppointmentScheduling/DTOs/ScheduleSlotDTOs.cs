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
/// Request để tạo hàng loạt ca tăng ca (17:00-20:00).
/// </summary>
public sealed class CreateOvertimeSlotsRequest
{
    public DateOnly VisitDate { get; init; }
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
/// Thông tin booking bên trong ScheduleSlotResponse.
/// Hiển thị patient name khi slot đã được book.
/// </summary>
public sealed class BookedAppointmentInfo
{
    public Guid AppointmentId { get; init; }
    public Guid PatientProfileId { get; init; }
    public string PatientFullName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public AppointmentStatus Status { get; init; }
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

    /// <summary>Danh sách booking chi tiết (chỉ có BOOKED appointments).</summary>
    public IReadOnlyList<BookedAppointmentInfo> BookedAppointments { get; init; } = Array.Empty<BookedAppointmentInfo>();

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