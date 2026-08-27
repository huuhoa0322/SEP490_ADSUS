using ADSUS_BE.DAL.Entities;
using ADSUS_BE.BLL.Engagement.Services;

namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Một lượt hội thoại (1 USER message + 1 ASSISTANT response).
/// Dùng để gửi history context cho LLM.
/// </summary>
public sealed record ChatTurn(ChatRole Role, string Content);

/// <summary>
/// Request để gửi tin nhắn cho chatbot (Patient).
/// Content: 1-1000 ký tự. Validate ở handler.
/// </summary>
public sealed class SendChatMessageRequest
{
    public required string Content { get; init; }
}

/// <summary>
/// Response cho một tin nhắn trong hội thoại.
/// </summary>
public sealed class ChatMessageResponse
{
    public Guid MessageId { get; init; }
    public ChatRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// True khi message này là safety response (GB-02: không phải do LLM sinh ra).
    /// Client dùng flag này để render safety card thay vì assistant bubble thường.
    /// </summary>
    public bool IsSafetyResponse { get; init; }

    /// <summary>
    /// Intent đã detect từ tin nhắn USER gần nhất.
    /// Client dùng để hiển thị context badge + suggestion chips.
    /// Null khi IsSafetyResponse=true hoặc không xác định được intent.
    /// </summary>
    public ChatIntent? DetectedIntent { get; init; }
}

/// <summary>
/// Response cho lịch sử hội thoại.
/// </summary>
public sealed class ChatHistoryResponse
{
    public IReadOnlyList<ChatMessageResponse> Messages { get; init; } =
        Array.Empty<ChatMessageResponse>();
}
