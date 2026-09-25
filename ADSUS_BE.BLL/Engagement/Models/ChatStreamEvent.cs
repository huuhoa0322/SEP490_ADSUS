using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.Engagement.Models;

/// <summary>
/// Base class cho các Server-Sent Event (SSE) trong AI Chatbot pipeline.
/// </summary>
public abstract record ChatStreamEvent
{
    [JsonIgnore]
    public abstract string EventType { get; }
}

/// <summary>
/// Event "thinking" phát ra ngay khi các pre-checks (validation, rate limit, safety filter) thành công,
/// trước khi gọi LLM client.
/// Wire format: event: thinking\ndata: {"status":"thinking"}\n\n
/// </summary>
public record ChatThinkingEvent(string Status = "thinking") : ChatStreamEvent
{
    [JsonIgnore]
    public override string EventType => "thinking";

    [JsonPropertyName("status")]
    public string Status { get; init; } = Status;
}

/// <summary>
/// Event "delta" phát ra mỗi khi nhận được một chunk văn bản từ LLM client.
/// Wire format: event: delta\ndata: {"chunk":"..."}\n\n
/// </summary>
public record ChatDeltaEvent(string Chunk) : ChatStreamEvent
{
    [JsonIgnore]
    public override string EventType => "delta";

    [JsonPropertyName("chunk")]
    public string Chunk { get; init; } = Chunk;

    [JsonIgnore]
    public string Text => Chunk;
}

/// <summary>
/// Event "done" phát ra khi kết thúc stream (hoặc phát ngay lập tức khi safety/rate limit chặn).
/// Chứa thông tin hoàn chỉnh của assistant message đã được lưu trong DB.
/// Wire format: event: done\ndata: {"messageId":"...","content":"...","detectedIntent":"...","isSafetyResponse":...}\n\n
/// </summary>
public record ChatDoneEvent : ChatStreamEvent
{
    [JsonIgnore]
    public override string EventType => "done";

    [JsonPropertyName("messageId")]
    public Guid MessageId { get; init; }

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("detectedIntent")]
    public string? DetectedIntent { get; init; }

    [JsonPropertyName("isSafetyResponse")]
    public bool IsSafetyResponse { get; init; }

    [JsonPropertyName("isRateLimitExceeded")]
    public bool IsRateLimitExceeded { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = "Assistant";

    [JsonIgnore]
    public ChatMessageResponse Response => new()
    {
        MessageId = MessageId,
        Content = Content,
        Role = Enum.TryParse<ChatRole>(Role, true, out var role) ? role : ChatRole.Assistant,
        CreatedAt = CreatedAt,
        IsSafetyResponse = IsSafetyResponse,
        IsRateLimitExceeded = IsRateLimitExceeded,
        DetectedIntent = Enum.TryParse<ChatIntent>(DetectedIntent, true, out var parsedIntent) ? parsedIntent : null
    };

    public ChatDoneEvent() { }

    public ChatDoneEvent(
        Guid messageId,
        string content,
        string? detectedIntent,
        bool isSafetyResponse,
        bool isRateLimitExceeded = false,
        DateTime? createdAt = null,
        string role = "Assistant")
    {
        MessageId = messageId;
        Content = content;
        DetectedIntent = detectedIntent;
        IsSafetyResponse = isSafetyResponse;
        IsRateLimitExceeded = isRateLimitExceeded;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        Role = role;
    }

    public ChatDoneEvent(ChatMessageResponse response)
    {
        MessageId = response.MessageId;
        Content = response.Content;
        Role = response.Role.ToString();
        CreatedAt = response.CreatedAt;
        IsSafetyResponse = response.IsSafetyResponse;
        DetectedIntent = response.DetectedIntent?.ToString();
        IsRateLimitExceeded = response.IsRateLimitExceeded;
    }
}

/// <summary>
/// Event "error" phát ra khi xảy ra lỗi trong quá trình streaming.
/// Wire format: event: error\ndata: {"message":"...","code":"STREAM_ERROR"}\n\n
/// </summary>
public record ChatErrorEvent(string Message, string? Code = "STREAM_ERROR") : ChatStreamEvent
{
    [JsonIgnore]
    public override string EventType => "error";

    [JsonPropertyName("message")]
    public string Message { get; init; } = Message;

    [JsonPropertyName("code")]
    public string? Code { get; init; } = Code;
}

// Aliases for alternate naming conventions
public sealed record ThinkingStreamEvent : ChatThinkingEvent
{
    public ThinkingStreamEvent(string status = "thinking") : base(status) { }
}

public sealed record DeltaStreamEvent : ChatDeltaEvent
{
    public DeltaStreamEvent(string chunk) : base(chunk) { }
}

public sealed record DoneStreamEvent : ChatDoneEvent
{
    public DoneStreamEvent(ChatMessageResponse response) : base(response) { }
    public DoneStreamEvent(
        Guid messageId,
        string content,
        string? detectedIntent,
        bool isSafetyResponse,
        bool isRateLimitExceeded = false,
        DateTime? createdAt = null,
        string role = "Assistant")
        : base(messageId, content, detectedIntent, isSafetyResponse, isRateLimitExceeded, createdAt, role) { }
}

public sealed record ErrorStreamEvent : ChatErrorEvent
{
    public ErrorStreamEvent(string message, string? code = "STREAM_ERROR") : base(message, code) { }
}
