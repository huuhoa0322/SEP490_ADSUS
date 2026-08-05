using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho ScheduleSlot (Module 8 — Appointment Scheduling, UC-15).
/// BR-01: slot không được trong quá khứ; range > 15 phút; không overlap cùng Doctor.
/// BR-02: Closed là trạng thái terminal.
/// </summary>
public interface IScheduleSlotRepository
{
    /// <summary>Lấy slot theo ID (kèm Doctor + Appointments).</summary>
    Task<ScheduleSlot?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lấy slot theo ID để update (tracking enabled).</summary>
    Task<ScheduleSlot?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Danh sách slot trong 1 ngày cụ thể, lọc theo Doctor (optional) và trạng thái (optional).</summary>
    Task<IReadOnlyList<ScheduleSlot>> ListByDateAsync(
        DateOnly slotDate,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        CancellationToken ct = default);

    /// <summary>Danh sách slot trong khoảng [from..to] (dùng cho calendar tuần/tháng).</summary>
    Task<IReadOnlyList<ScheduleSlot>> ListByRangeAsync(
        DateOnly from,
        DateOnly to,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra overlap slot cho cùng Doctor trong cùng ngày.
    /// Overlap = hai slot có khoảng thời gian giao nhau (start &lt; other.end && end &gt; other.start).
    /// Loại trừ chính slot truyền vào (dùng khi update).
    /// </summary>
    Task<bool> HasOverlapAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? excludeSlotId = null,
        CancellationToken ct = default);

    /// <summary>Đếm số appointment đang Booked trên slot này (dùng khi close slot có booking).</summary>
    Task<int> CountActiveAppointmentsAsync(Guid slotId, CancellationToken ct = default);

    /// <summary>Tạo slot mới (status = OPEN).</summary>
    Task<ScheduleSlot> AddAsync(ScheduleSlot slot, CancellationToken ct = default);

    /// <summary>Update slot (dùng khi close).</summary>
    Task UpdateAsync(ScheduleSlot slot, CancellationToken ct = default);
}
