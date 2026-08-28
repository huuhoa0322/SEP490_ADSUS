namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Thông tin dị ứng của bệnh nhân — dùng chatbot nhắc khi tư vấn thuốc mới.
/// </summary>
public sealed record AllergyContextDto(
    Guid Id,
    string AllergyTypeName,
    string? Note);

/// <summary>
/// Thông tin bệnh nền của bệnh nhân — dùng chatbot khi tư vấn thuốc hoặc lối sống.
/// </summary>
public sealed record DiseaseContextDto(
    Guid Id,
    string DiseaseName,
    string? Note);
