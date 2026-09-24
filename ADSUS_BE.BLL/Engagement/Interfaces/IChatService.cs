using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Models;

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
    /// Stream tin nhắn từ chatbot theo thời gian thực qua Server-Sent Events (SSE).
    /// </summary>
    IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
        Guid userId,
        string content,
        CancellationToken ct = default);

    /// <summary>
    /// Stream tin nhắn từ chatbot theo thời gian thực qua Server-Sent Events (SSE) với request object.
    /// </summary>
    IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
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
