using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho Appointment (Module 8 — UC-13, UC-14).
/// Patient đặt lịch và xem lịch hẹn của mình.
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>
    /// Danh sách appointment của một bệnh nhân.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListByPatientAsync(
        Guid patientProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Danh sách appointment của một Doctor trong khoảng ngày (theo Slot.SlotDate).
    /// Dùng cho màn "Lịch bệnh nhân" (Doctor xem ai đã đặt lịch với mình) — độc lập với
    /// luồng quản lý ScheduleSlot.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListByDoctorAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy appointment theo ID (kèm Slot + Doctor).
    /// </summary>
    Task<Appointment?> GetByIdAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Tạo appointment mới.
    /// </summary>
    Task<Appointment> CreateAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>
    /// Update appointment (dùng khi cancel).
    /// </summary>
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>
    /// Lịch hẹn của một hồ sơ bệnh nhân HOẶC do một tài khoản đặt hộ (màn "Lịch khám của tôi"),
    /// mới nhất trước. Chỉ đọc.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListForPatientOrBookerAsync(
        Guid patientProfileId,
        Guid? bookedByUserId,
        AppointmentStatus? statusFilter,
        CancellationToken ct = default);

    /// <summary>
    /// Số lịch hẹn một tài khoản đã huỷ kể từ <paramref name="sinceUtc"/> (tự đặt hoặc đặt hộ) —
    /// KHÔNG tính lịch huỷ do đổi lịch ("Đổi lịch:") hay do bác sĩ nghỉ phép. Dùng cho luật
    /// chống lạm dụng huỷ lịch.
    /// </summary>
    Task<int> CountUserCancellationsSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>
    /// Hàng chờ check-in trong khoảng ngày. <paramref name="statusFilter"/> null +
    /// <paramref name="excludeCancelled"/> true là màn mặc định (ẩn ca đã huỷ). Chỉ đọc.
    /// </summary>
    Task<CheckinQueuePage> GetCheckinQueuePageAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string? search,
        AppointmentStatus? statusFilter,
        bool excludeCancelled,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Ca kế tiếp trong ngày của bác sĩ (Booked hoặc đã check-in, ca khám chưa kết luận),
    /// sớm nhất trước. Chỉ đọc.
    /// </summary>
    Task<Appointment?> GetNextForDoctorOnDateAsync(Guid doctorId, DateOnly date, CancellationToken ct = default);

    /// <summary>Đọc để cập nhật — CÓ tracking, kèm Slot + Doctor của slot.</summary>
    Task<Appointment?> GetByIdForUpdateAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Đọc để cập nhật — CÓ tracking, kèm Slot + Doctor, hồ sơ bệnh nhân + tài khoản, người đặt hộ
    /// và mối quan hệ (đủ để kiểm tra quyền sửa và dựng response).
    /// </summary>
    Task<Appointment?> GetByIdWithDetailsForUpdateAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Lịch hẹn đang Booked mới nhất của một Case (Case từng đổi lịch thì có nhiều lịch hẹn) —
    /// CÓ tracking, kèm Slot + Doctor.
    /// </summary>
    Task<Appointment?> GetLatestBookedByCaseForUpdateAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Lịch hẹn mới nhất của một Case, bất kể trạng thái. Chỉ đọc.</summary>
    Task<Appointment?> GetLatestByCaseAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Đọc lịch hẹn cũ để đổi lịch — CÓ tracking, kèm Slot + Doctor, hồ sơ bệnh nhân + tài khoản
    /// và Case (trạng thái Case quyết định tình huống đổi lịch).
    /// </summary>
    Task<Appointment?> GetForRescheduleAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>Người đặt hộ + mối quan hệ của một lịch hẹn (để dựng response). Chỉ đọc.</summary>
    Task<Appointment?> GetWithBookerAndRelationshipAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Lịch hẹn đầu tiên của hồ sơ bệnh nhân trong một ngày khám, ở một trong các trạng thái cho
    /// trước (luật "mỗi ngày tối đa 1 lịch"), kèm Slot + Doctor. Chỉ đọc.
    /// </summary>
    Task<Appointment?> GetFirstOnDateAsync(
        Guid patientProfileId,
        DateOnly slotDate,
        IReadOnlyCollection<AppointmentStatus> statuses,
        Guid? excludeAppointmentId,
        CancellationToken ct = default);

    /// <summary>Số lịch hẹn đang Booked của một hồ sơ bệnh nhân (mọi người đặt).</summary>
    Task<int> CountBookedByProfileAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Số lịch hẹn đang Booked hồ sơ tự đặt cho chính mình (không qua đặt hộ).</summary>
    Task<int> CountSelfBookedByProfileAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Số lịch hẹn đang Booked một tài khoản đã đặt hộ người thân.</summary>
    Task<int> CountBookedForOthersByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Số lịch hẹn của một hồ sơ bệnh nhân bị huỷ kể từ <paramref name="sinceUtc"/> — cùng quy
    /// tắc loại trừ như <see cref="CountUserCancellationsSinceAsync"/>.
    /// </summary>
    Task<int> CountProfileCancellationsSinceAsync(Guid patientProfileId, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Hồ sơ bệnh nhân đã có lịch hẹn còn hiệu lực (không huỷ, không vắng) trong slot này chưa.</summary>
    Task<bool> ExistsActiveInSlotAsync(Guid patientProfileId, Guid slotId, CancellationToken ct = default);

    /// <summary>Thêm lịch hẹn vào context, CHƯA lưu — dùng trong transaction của Service.</summary>
    Task AddAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>Lưu mọi thay đổi đang được track — Service quyết định lúc lưu (L3 §8).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
