namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Safety check trước khi gọi LLM (Module 10 Chat) — CLAUDE.md §3.2 + GB-02.
/// AI không thay thế bác sĩ tâm lý. Nếu user input chứa từ khóa tâm lý nhạy cảm
/// → trả safety response (text có sẵn), KHÔNG gọi LLM.
///
/// Implementation: case-insensitive substring match với 15 keyword đã chốt ở §3.2.
/// KHÔNG dùng LLM để filter (anti-pattern §7.1) — tốn token + thêm latency + có thể
/// miss khi LLM hallucinate.
/// </summary>
public interface IPsychologyTopicFilter
{
    /// <summary>
    /// Check input có chứa psychology topic nhạy cảm không.
    /// </summary>
    /// <returns>
    /// null nếu input an toàn → cho phép gọi LLM.
    /// Tên topic nếu phát hiện (VD: "trầm cảm") → trả safety response, KHÔNG gọi LLM.
    /// </returns>
    string? DetectUnsafeTopic(string userMessage);
}