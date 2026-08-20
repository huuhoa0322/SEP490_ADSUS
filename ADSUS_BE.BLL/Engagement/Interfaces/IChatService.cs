using ADSUS_BE.BLL.Engagement.DTOs;

namespace ADSUS_BE.BLL.Engagement.Interfaces;

/// <summary>
/// Service orchestration cho Module 10 Chat (FT-39).
/// Điều phối: PsychologyTopicFilter → IChatClient (hoặc safety response) → IAiChatMessageRepository.
/// GB-02: KHÔNG gọi LLM khi PsychologyTopicFilter phát hiện từ khóa nhạy cảm.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Patient gửi tin nhắn → nhận phản hồi.
    /// Side effect: lưu USER + ASSISTANT message vào DB.
    /// </summary>
    /// <param name="userId">ID tài khoản (từ JWT).</param>
    /// <param name="request">Tin nhắn cần gửi.</param>
    /// <param name="ct">CancellationToken.</param>
    Task<ChatMessageResponse> SendMessageAsync(
        Guid userId,
        SendChatMessageRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử hội thoại (phân trang theo from/to).
    /// </summary>
    Task<ChatHistoryResponse> GetHistoryAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken ct = default);
}
