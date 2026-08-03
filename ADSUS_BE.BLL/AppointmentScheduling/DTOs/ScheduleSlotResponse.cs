namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

public sealed record ScheduleSlotResponse(
    Guid SlotId,
    Guid DoctorId,
    string DoctorName,
    DateOnly SlotDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);