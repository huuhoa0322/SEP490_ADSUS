using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.AppointmentScheduling.DTOs;

// ─── Symptom Input (từ Mobile booking) ───────────────────────────────────────

/// <summary>
/// Input triệu chứng khi đặt lịch khám.
/// </summary>
public sealed class SymptomInput
{
    public Guid CategoryId { get; init; }
    public Guid? SymptomId { get; init; }
    public string? OtherNote { get; init; }
}

// ─── Requests ──────────────────────────────────────────────────────────────────

/// <summary>
/// Request để đặt lịch hẹn (UC-13).
/// BR-01: Slot phải tồn tại và có status = OPEN.
/// BR-02: Patient không được đặt trùng slot đã có BOOKED appointment.
/// </summary>
public sealed class BookAppointmentRequest
{
    public Guid ScheduleSlotId { get; init; }
    public string? Reason { get; init; }

    /// <summary>
    /// Danh sách triệu chứng (tùy chọn). Nếu có, hệ thống sẽ tạo Case với status=BOOKED.
    /// </summary>
    public List<SymptomInput>? Symptoms { get; init; }
}

/// <summary>
/// Request để hủy lịch hẹn (UC-14).
/// BR-01: Chỉ patient sở hữu appointment mới được hủy.
/// BR-02: Lý do hủy bắt buộc.
/// </summary>
public sealed class CancelAppointmentRequest
{
    public string CancellationReason { get; init; } = string.Empty;
}

// ─── Responses ─────────────────────────────────────────────────────────────────

/// <summary>
/// Response chi tiết cho appointment (UC-13, UC-14).
/// </summary>
public sealed class AppointmentResponse
{
    public Guid AppointmentId { get; init; }
    public Guid ScheduleSlotId { get; init; }
    public DateOnly SlotDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public AppointmentStatus Status { get; init; }
    public string? Reason { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime? CalendarSyncedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Case được tạo tự động khi đặt lịch (nếu có triệu chứng).
    /// </summary>
    public Guid? CaseId { get; init; }
}

/// <summary>
/// Response summary cho danh sách lịch hẹn của tôi (UC-14).
/// </summary>
public sealed class AppointmentSummaryResponse
{
    public Guid AppointmentId { get; init; }
    public DateOnly SlotDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public AppointmentStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Reason { get; init; }
    public string? CancellationReason { get; init; }
}

/// <summary>
/// Response cho slot mở (UC-13 BR-02: patient chỉ thấy slot OPEN).
/// </summary>
public sealed class OpenSlotResponse
{
    public Guid SlotId { get; init; }
    public Guid DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;

    /// <summary>
    /// Trạng thái tài khoản bác sĩ — dùng để mobile filter bác sĩ active.
    /// Chỉ slot của bác sĩ ACTIVE được trả về cho patient.
    /// </summary>
    public UserStatus DoctorStatus { get; init; }

    public DateOnly SlotDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public DateTime CreatedAt { get; init; }
}
