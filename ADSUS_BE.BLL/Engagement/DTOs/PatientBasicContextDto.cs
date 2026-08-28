namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Thông tin cơ bản bệnh nhân — tên và ngày sinh (dùng để chatbot xưng hô đúng).
/// </summary>
public sealed record PatientBasicContextDto(
    string FullName,
    DateOnly? DateOfBirth,
    int? Age);
