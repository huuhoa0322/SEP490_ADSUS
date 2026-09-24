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

    /// <summary>
    /// Khung giờ (DoctorId, SlotDate, StartTime, EndTime) của mọi slot thuộc các bác sĩ cho trước
    /// trong khoảng ngày, ở MỌI trạng thái — cùng quy tắc với <see cref="HasOverlapAsync"/>. Dùng
    /// khi cần kiểm tra trùng giờ cho hàng loạt slot trong bộ nhớ (JOB-02). Chỉ đọc, chỉ có 4 cột.
    /// </summary>
    Task<IReadOnlyList<ScheduleSlot>> ListTimeRangesAsync(
        IReadOnlyCollection<Guid> doctorIds,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    /// <summary>Đếm số appointment đang Booked trên slot này (dùng khi close slot có booking).</summary>
    Task<int> CountActiveAppointmentsAsync(Guid slotId, CancellationToken ct = default);

    /// <summary>Tạo slot mới (status = OPEN).</summary>
    Task<ScheduleSlot> AddAsync(ScheduleSlot slot, CancellationToken ct = default);

    /// <summary>Tạo hàng loạt slot trong 1 SaveChanges duy nhất — dùng khi sinh nhiều slot cùng
    /// lúc (EnsureDefaultSlotsAsync) để tránh 1 round-trip DB / slot.</summary>
    Task AddRangeAsync(IEnumerable<ScheduleSlot> slots, CancellationToken ct = default);

    /// <summary>Update slot (dùng khi close).</summary>
    Task UpdateAsync(ScheduleSlot slot, CancellationToken ct = default);

    /// <summary>
    /// Slot đang Booked của bác sĩ trong một ngày, bắt đầu SAU <paramref name="afterTime"/> —
    /// ứng viên để tái chế về Open khi ca trước kết thúc sớm. CÓ tracking (Service sẽ cập nhật
    /// Status rồi gọi <see cref="SaveChangesAsync"/>). Kèm Appointments + Case.
    /// </summary>
    Task<IReadOnlyList<ScheduleSlot>> ListBookedForDoctorAfterForUpdateAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly afterTime,
        CancellationToken ct = default);

    /// <summary>
    /// Slot chưa Closed của bác sĩ trong một ngày, nằm trọn trong [<paramref name="from"/>,
    /// <paramref name="to"/>] — CÓ tracking, kèm Appointments. Dùng khi duyệt đơn nghỉ phép: đóng
    /// slot và huỷ lịch hẹn trên đó rồi <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<ScheduleSlot>> ListNotClosedWithinForUpdateAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly from,
        TimeOnly to,
        CancellationToken ct = default);

    /// <summary>
    /// Slot của một bác sĩ trong khoảng ngày, kèm Appointments (không kèm Doctor/Case/hồ sơ như
    /// <see cref="ListByRangeAsync"/>). Chỉ đọc — dùng cho bảng tổng hợp ca làm theo tháng.
    /// </summary>
    Task<IReadOnlyList<ScheduleSlot>> ListWithAppointmentsForDoctorAsync(
        Guid doctorId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    /// <summary>
    /// Bỏ theo dõi mọi slot đang track — dùng sau khi lưu hàng loạt thất bại, để các slot chưa
    /// lưu được không bị gửi lại ở lần SaveChanges kế tiếp.
    /// </summary>
    void DetachTracked();

    /// <summary>Lưu mọi thay đổi đang được track — Service quyết định lúc lưu (L3 §8).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
