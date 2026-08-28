namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Một liều thuốc trong lịch hôm nay — dùng cho chatbot trả lời
/// "hôm nay tôi cần uống những thuốc gì", "thuốc này uống lúc nào".
/// </summary>
public sealed record TodayIntakeContextDto(
    Guid IntakeId,
    string MedicineName,
    string Dosage,
    string? Instructions,
    DateTime ScheduledTime,
    string Status);
