namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Một dòng nhật ký sức khỏe — dùng chatbot trả lời
/// "nhật ký sức khỏe gần đây của tôi như thế nào".
/// Giới hạn: 7 ngày gần nhất.
/// </summary>
public sealed record HealthLogContextDto(
    DateOnly LogDate,
    string Content);
