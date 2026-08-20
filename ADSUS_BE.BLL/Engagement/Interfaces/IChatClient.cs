using ADSUS_BE.BLL.Engagement.DTOs;

namespace ADSUS_BE.BLL.Engagement.Interfaces;

/// <summary>
/// Interface cho LLM provider (swappable — OpenAI, Anthropic, v.v.).
/// Current implementation: OpenAiChatClient (gpt-4o-mini).
/// Test stub: FakeChatClient.
/// </summary>
public interface IChatClient
{
    /// <summary>
    /// Gửi tin nhắn cho LLM và nhận phản hồi.
    /// </summary>
    /// <param name="systemPrompt">System prompt định nghĩa vai trò trợ lý.</param>
    /// <param name="history">Lịch sử hội thoại (từ DB).</param>
    /// <param name="userMessage">Tin nhắn mới của user.</param>
    /// <param name="ct">CancellationToken.</param>
    /// <returns>Nội dung phản hồi của LLM (chưa ghép disclaimer).</returns>
    Task<string> SendMessageAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken ct = default);
}
