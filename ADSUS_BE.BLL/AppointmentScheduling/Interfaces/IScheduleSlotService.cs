using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.Interfaces;

/// <summary>
/// Service cho ScheduleSlot (Module 8 — Appointment Scheduling, UC-15).
/// BR-01: slot không trong quá khứ; range > 15 phút; không overlap.
/// BR-02: Closed là terminal; không thể reopen.
/// </summary>
public interface IScheduleSlotService
{
    Task<IReadOnlyList<ScheduleSlotResponse>> ListSlotsAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        CancellationToken ct = default);

    Task<ScheduleSlotResponse?> GetSlotAsync(Guid slotId, CancellationToken ct = default);

    Task<ScheduleSlotResponse> CreateSlotAsync(
        CreateScheduleSlotRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Close slot. Trả về CloseSlotImpactResponse nếu slot có booking để FE hiển thị cảnh báo.
    /// Nếu forceClose = false và có booking, throw InvalidOperationException.
    /// </summary>
    Task<CloseSlotImpactResponse> CloseSlotAsync(
        Guid slotId,
        bool forceClose,
        CancellationToken ct = default);
}