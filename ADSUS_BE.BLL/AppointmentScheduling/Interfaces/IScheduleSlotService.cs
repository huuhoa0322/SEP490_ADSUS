using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.Interfaces;

/// <summary>
/// Service cho ScheduleSlot (Module 8 — Appointment Scheduling, UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); range > 15 phút; không overlap.
/// BR-02: Closed là terminal.
/// Doctor tự quản lý lịch của chính mình; hệ thống tự sinh ca mặc định T2-T6.
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

    /// <summary>Tạo slot cho doctor (DoctorId truyền từ controller, lấy từ JWT).</summary>
    Task<ScheduleSlotResponse> CreateSlotAsync(
        Guid doctorId,
        CreateScheduleSlotRequest request,
        CancellationToken ct = default);

    /// <summary>Sửa giờ slot (tách ca 8h-12h thành 8h-10h + 10h-12h).</summary>
    Task<ScheduleSlotResponse> UpdateSlotAsync(
        Guid slotId,
        UpdateScheduleSlotRequest request,
        CancellationToken ct = default);

    /// <summary>Đóng slot. Nếu có booking và forceClose=false, trả CloseSlotImpactResponse.</summary>
    Task<CloseSlotImpactResponse> CloseSlotAsync(
        Guid slotId,
        bool forceClose,
        CancellationToken ct = default);

    /// <summary>
    /// Đảm bảo Doctor có ca mặc định (8h-12h, 13h-17h, T2-T6) cho tuần bắt đầu weekStart (T2).
    /// Idempotent: nếu đã có slot OPEN overlap range thì skip. Doctor tách ca → không backfill.
    /// </summary>
    Task EnsureDefaultSlotsAsync(
        Guid doctorId,
        DateOnly weekStart,
        CancellationToken ct = default);
}
