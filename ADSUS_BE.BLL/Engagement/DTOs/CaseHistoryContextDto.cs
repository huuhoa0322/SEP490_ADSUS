namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Lịch sử ca khám gần đây — dùng chatbot trả lời
/// "lần khám gần nhất của tôi chẩn đoán gì".
/// Giới hạn: 3 ca gần nhất.
/// </summary>
public sealed record CaseHistoryContextDto(
    Guid CaseId,
    DateOnly VisitDate,
    string? FinalDiagnosis,
    string? DoctorConclusion,
    string DoctorName);
