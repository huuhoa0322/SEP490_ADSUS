namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Lịch hẹn sắp tới của bệnh nhân — dùng chatbot trả lời
/// "khi nào tôi có lịch khám tiếp theo", "lịch khám ngày nào".
/// Giới hạn: 3 lịch hẹn BOOKED sắp tới nhất.
/// </summary>
public sealed record UpcomingAppointmentContextDto(
    Guid AppointmentId,
    DateOnly SlotDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string DoctorName,
    string? Reason);
