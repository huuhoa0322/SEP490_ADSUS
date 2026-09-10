using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.Interfaces;

/// <summary>
/// Service cho Appointment (Module 8 — UC-13, UC-14).
/// Patient đặt lịch hẹn và xem/hủy lịch hẹn của mình.
/// </summary>
public interface IAppointmentService
{
    /// <summary>
    /// Danh sách slot còn trống (status = OPEN) cho bệnh nhân đặt lịch.
    /// BR-02: Chỉ trả về slot OPEN (không có appointment BOOKED).
    /// Giới hạn: chỉ trả về slots trong vòng 2 tuần.
    /// </summary>
    Task<IReadOnlyList<OpenSlotResponse>> ListOpenSlotsAsync(
        string? doctorId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Danh sách lịch hẹn của bệnh nhân.
    /// </summary>
    Task<IReadOnlyList<AppointmentSummaryResponse>> ListMyAppointmentsAsync(
        Guid patientProfileId,
        AppointmentStatus? statusFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Chi tiết một lịch hẹn.
    /// </summary>
    Task<AppointmentResponse?> GetByIdAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Đặt lịch hẹn mới (UC-13).
    /// BR-01: Slot phải tồn tại và có status = OPEN.
    /// BR-02: Kiểm tra không trùng booking.
    /// </summary>
    Task<AppointmentResponse> BookAppointmentAsync(
        Guid patientProfileId,
        BookAppointmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Bác sĩ hẹn tái khám cho bệnh nhân (UC-15 mở rộng).
    /// </summary>
    Task<AppointmentResponse> CreateFollowUpAppointmentAsync(
        Guid doctorId,
        FollowUpAppointmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Hủy lịch hẹn (UC-14).
    /// BR-01: Chỉ patient sở hữu mới được hủy.
    /// BR-02: Lý do hủy bắt buộc.
    /// </summary>
    Task<AppointmentResponse> CancelAppointmentAsync(
        Guid appointmentId,
        Guid patientProfileId,
        CancelAppointmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Nurse checkin appointment khi bệnh nhân đến bệnh viện.
    /// Appointment: Booked → Approved
    /// </summary>
    Task<AppointmentResponse> CheckinAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Nurse checkin appointment thông qua caseId.
    /// Tìm appointment đang BOOKED liên quan đến case, rồi checkin.
    /// Dùng khi nurse có CaseId (từ đăng ký) thay vì AppointmentId.
    /// </summary>
    Task<AppointmentResponse> CheckinByCaseIdAsync(
        Guid caseId,
        CancellationToken ct = default);

    /// <summary>
    /// Danh sách bệnh nhân BOOKED với Doctor trong khoảng ngày — cho màn "Lịch bệnh nhân".
    /// Chỉ Booked; Cancelled, Completed và các trạng thái khác bị lọc bỏ hoàn toàn.
    /// Bác sĩ không cần theo dõi việc bệnh nhân đã đến hay chưa qua Appointment —
    /// việc đó do Nurse xử lý qua checkin; bác sĩ chỉ quản lý case khám (theo dõi
    /// tiến trình qua Case.Status).
    /// </summary>
    Task<IReadOnlyList<DoctorPatientAppointmentResponse>> ListForDoctorAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách appointments trong ngày đang chờ check-in cho Nurse (tương thích ngược).
    /// Trả về Booked và Approved appointments, sắp xếp theo giờ.
    /// </summary>
    Task<CheckinQueueResponse> GetCheckinQueueAsync(
        DateOnly date,
        string? search = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách appointments chờ check-in theo khoảng thời gian, trạng thái, tìm kiếm và phân trang.
    /// </summary>
    Task<CheckinQueueResponse> GetCheckinQueueAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 15,
        CancellationToken ct = default);

    /// <summary>
    /// Đổi lịch hoặc tái đặt lịch hẹn bởi Nurse/Admin/Receptionist (Milestone 1).
    /// Hỗ trợ 3 kịch bản: Booked (đổi lịch), Completed/Approved (tái đặt lịch), Cancelled/NoShow (khôi phục).
    /// </summary>
    Task<AppointmentResponse> RescheduleAppointmentAsync(
        Guid oldAppointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken ct = default);
}
