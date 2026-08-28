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
}
