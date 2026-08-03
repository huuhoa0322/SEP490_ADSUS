using ADSUS_BE.BLL.AppointmentScheduling.DTOs;

namespace ADSUS_BE.BLL.AppointmentScheduling.Interfaces;

public interface IScheduleSlotService
{
    Task<PagedResult<ScheduleSlotResponse>> SearchAsync(
        Guid? doctorId,
        DateOnly? slotDate,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ScheduleSlotResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ScheduleSlotResponse> CreateAsync(
        CreateScheduleSlotRequest req,
        CancellationToken ct = default);

    Task<ScheduleSlotResponse> UpdateStatusAsync(
        Guid id,
        UpdateScheduleSlotStatusRequest req,
        CancellationToken ct = default);

    Task<PagedResult<AppointmentSummaryResponse>> ListAppointmentsBySlotAsync(
        Guid slotId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}