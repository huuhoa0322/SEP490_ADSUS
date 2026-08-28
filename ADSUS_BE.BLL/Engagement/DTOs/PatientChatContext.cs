namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Tổng hợp toàn bộ dữ liệu bệnh nhân để inject vào system prompt chatbot.
/// Mỗi trường nullable vì patient có thể chưa có dữ liệu ở section nào đó.
/// </summary>
public sealed record PatientChatContext(
    PatientBasicContextDto? BasicInfo,
    IReadOnlyList<PrescriptionContextDto>? ActivePrescriptions,
    IReadOnlyList<TodayIntakeContextDto>? TodayIntakes,
    IReadOnlyList<UpcomingAppointmentContextDto>? UpcomingAppointments,
    IReadOnlyList<CaseHistoryContextDto>? RecentCases,
    IReadOnlyList<AllergyContextDto>? Allergies,
    IReadOnlyList<DiseaseContextDto>? Diseases,
    IReadOnlyList<HealthLogContextDto>? RecentHealthLogs,
    IReadOnlyList<BlogPostListItemResponse>? RecentBlogs);
