using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Services;

namespace ADSUS_BE.BLL.Engagement.Interfaces;

/// <summary>
/// Tổng hợp toàn bộ dữ liệu bệnh nhân phục vụ chatbot.
/// Dùng cho RAG pattern: query DB → inject vào system prompt LLM.
///
/// Tất cả query AsNoTracking, chỉ đọc, không sửa entity.
/// Thực thi song song (Task.WhenAll) để giảm latency.
/// Selective query: chỉ query sources cần thiết dựa trên intent.
/// </summary>
public interface IChatDataAggregator
{
    /// <summary>
    /// Tổng hợp data sources cần thiết cho một bệnh nhân dựa trên intent.
    /// Trả về null context nếu bệnh nhân chưa có hồ sơ nền.
    /// Khi intent là Greeting hoặc General (không có TriggeredSources), trả về
    /// context chỉ với BasicInfo (tên, tuổi).
    /// </summary>
    /// <param name="userId">Tài khoản bệnh nhân (từ JWT).</param>
    /// <param name="intent">Intent đã detect từ user message.</param>
    /// <param name="ct">CancellationToken.</param>
    Task<PatientChatContext?> BuildContextAsync(Guid userId, IntentResult intent, CancellationToken ct = default);
}
