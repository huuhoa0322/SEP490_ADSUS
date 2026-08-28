namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Phiên bản rút gọn của đơn thuốc dùng cho chatbot prompt.
/// Chỉ lấy thông tin cần thiết để LLM trả lời câu hỏi về thuốc.
/// Giới hạn: 2 đơn gần nhất, 5 items mỗi đơn.
/// </summary>
public sealed record PrescriptionContextDto(
    Guid PrescriptionId,
    DateOnly PrescribedDate,
    string? GeneralNote,
    IReadOnlyList<PrescriptionItemContextDto> Items);

/// <summary>
/// Phiên bản rút gọn của một item trong đơn thuốc.
/// </summary>
public sealed record PrescriptionItemContextDto(
    string MedicineName,
    string Dosage,
    string? Instructions,
    short DurationDays,
    DateOnly StartDate);
