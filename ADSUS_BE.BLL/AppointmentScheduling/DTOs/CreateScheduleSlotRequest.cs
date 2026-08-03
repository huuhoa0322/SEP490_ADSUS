namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

public sealed record CreateScheduleSlotRequest(
    Guid DoctorId,
    DateOnly SlotDate,
    string StartTime,
    string EndTime);