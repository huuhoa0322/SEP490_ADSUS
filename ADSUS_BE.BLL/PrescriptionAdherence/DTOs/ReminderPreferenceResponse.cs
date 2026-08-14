namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Response cho GET /api/v1/me/reminder-preference.
/// Trả về preference hiện tại của bệnh nhân (hoặc default nếu chưa có dòng).
/// </summary>
public sealed record ReminderPreferenceResponse(
    bool NotifEnabled,
    string MorningTime,
    string MiddayTime,
    string EveningTime);

/// <summary>
/// Request cho PUT /api/v1/me/reminder-preference.
/// Giờ gửi dạng "HH:mm" (ví dụ "07:30").
/// </summary>
public sealed record UpdateReminderPreferenceRequest(
    bool? NotifEnabled,
    string? MorningTime,
    string? MiddayTime,
    string? EveningTime);
