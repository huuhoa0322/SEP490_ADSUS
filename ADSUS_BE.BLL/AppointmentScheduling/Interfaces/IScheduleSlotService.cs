using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.Interfaces;

/// <summary>
/// Service cho ScheduleSlot (Module 8 — Appointment Scheduling, UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); range > 15 phút; không overlap.
/// BR-02: Closed có thể mở lại (ReopenSlotAsync).
/// Doctor tự quản lý lịch của chính mình; hệ thống tự sinh ca mặc định T2-CN.
/// </summary>
public interface IScheduleSlotService
{
    /// <summary>
    /// Lấy danh sách slot với pagination.
    /// Trả về (items, totalCount) để controller tính pagination metadata.
    /// </summary>
    Task<(IReadOnlyList<ScheduleSlotResponse> Items, int TotalCount)> ListSlotsAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        int page = 1,
        int pageSize = 200, // 14 ngày × 16 slots = 224 max
        CancellationToken ct = default);

    Task<ScheduleSlotResponse?> GetSlotAsync(Guid slotId, CancellationToken ct = default);

    /// <summary>Tạo slot cho doctor (DoctorId truyền từ controller, lấy từ JWT).</summary>
    Task<ScheduleSlotResponse> CreateSlotAsync(
        Guid doctorId,
        CreateScheduleSlotRequest request,
        CancellationToken ct = default);

    Task<(int SuccessCount, int ErrorCount)> CreateOvertimeSlotsAsync(
        CreateOvertimeSlotsRequest request,
        Guid doctorId,
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

    /// <summary>Mở lại slot đã đóng.</summary>
    Task<ScheduleSlotResponse> ReopenSlotAsync(Guid slotId, CancellationToken ct = default);

    /// <summary>
    /// Đảm bảo Doctor có ca mặc định (8h-12h, 13h-17h, T2-CN) cho tuần bắt đầu weekStart (T2).
    /// Idempotent: nếu đã có slot OPEN overlap range thì skip. Doctor tách ca → không backfill.
    /// </summary>
    Task EnsureDefaultSlotsAsync(
        Guid doctorId,
        DateOnly weekStart,
        CancellationToken ct = default);

    /// <summary>
    /// Tự sinh ca mặc định T2-CN (8h-12h, 13h-17h) cho 21 ngày tới từ hôm nay.
    /// Duyệt từng ngày, với mỗi ca mặc định check overlap → nếu thiếu thì sinh.
    /// Idempotent: nếu đã có slot OPEN overlap thì skip.
    /// Logic: ngày 10 sinh đến 30, sang ngày 11 sẽ sinh thêm ca cho ngày 1 tháng sau.
    /// </summary>
    Task EnsureUpcomingSlotsAsync(
        Guid doctorId,
        CancellationToken ct = default);
}
