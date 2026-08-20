using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Orchestrator cho Module 10 Chat (FT-39).
///
/// Flow:
/// 1. Validate content (length ≤ 1000, not empty after trim)
/// 2. Save USER message
/// 3. Check PsychologyTopicFilter
///    - Unsafe → return safety response, save ASSISTANT with IsSafety=true
///    - Safe    → call LLM, append disclaimer, save ASSISTANT
/// 4. Return ChatMessageResponse
///
/// GB-02: KHÔNG gọi LLM khi filter chặn.
/// </summary>
public sealed class ChatService : IChatService
{
    private const int MaxContentLength = 1000;

    // System prompt mặc định theo docs/module-10.md
    private const string DefaultSystemPrompt =
        "Bạn là trợ lý sức khỏe của ADSUS. " +
        "Trả lời ngắn gọn, dựa trên kết quả khám đã được bác sĩ xác nhận. " +
        "Luôn kèm miễn trừ trách nhiệm.";

    private readonly IAiChatMessageRepository _repo;
    private readonly IPsychologyTopicFilter _psychologyFilter;
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatService> _logger;
    private readonly string _systemPrompt;

    public ChatService(
        IAiChatMessageRepository repo,
        IPsychologyTopicFilter psychologyFilter,
        IChatClient chatClient,
        ILogger<ChatService> logger,
        IOptions<AiBackendSettings> settings)
    {
        _repo = repo;
        _psychologyFilter = psychologyFilter;
        _chatClient = chatClient;
        _logger = logger;
        _systemPrompt = !string.IsNullOrWhiteSpace(settings.Value.ChatBotSystemPrompt)
            ? settings.Value.ChatBotSystemPrompt
            : DefaultSystemPrompt;
    }

    public async Task<ChatMessageResponse> SendMessageAsync(
        Guid userId,
        SendChatMessageRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate
        var content = request.Content.Trim();
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Tin nhắn không được để trống.");
        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Tin nhắn vượt quá {MaxContentLength} ký tự.");

        var now = DateTime.UtcNow;

        // 2. Save USER message
        var userMsg = new AiChatMessage
        {
            MessageId = Guid.NewGuid(),
            UserId = userId,
            Content = content,
            Role = ChatRole.User,
            CreatedAt = now,
        };
        await _repo.AddAsync(userMsg, ct);

        // 3. Check psychology filter
        var unsafeTopic = _psychologyFilter.DetectUnsafeTopic(content);
        string assistantContent;
        bool isSafety;

        if (unsafeTopic is not null)
        {
            // GB-02: safety — KHÔNG gọi LLM
            _logger.LogInformation(
                "PsychologyTopicFilter matched [{Topic}] for user {UserId}. Returning safety response.",
                unsafeTopic, userId);
            assistantContent = DisclaimerText.Safety;
            isSafety = true;
        }
        else
        {
            // Safe: gọi LLM
            var history = await BuildHistoryForLlm(userId, ct);
            var llmResponse = await _chatClient.SendMessageAsync(
                _systemPrompt, history, content, ct);
            assistantContent = llmResponse + DisclaimerText.General;
            isSafety = false;
        }

        // 4. Save ASSISTANT message
        var assistantMsg = new AiChatMessage
        {
            MessageId = Guid.NewGuid(),
            UserId = userId,
            Content = assistantContent,
            Role = ChatRole.Assistant,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.AddAsync(assistantMsg, ct);

        return new ChatMessageResponse
        {
            MessageId = assistantMsg.MessageId,
            Role = ChatRole.Assistant,
            Content = assistantContent,
            CreatedAt = assistantMsg.CreatedAt,
            IsSafetyResponse = isSafety,
        };
    }

    public async Task<ChatHistoryResponse> GetHistoryAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken ct = default)
    {
        // Clamp limit
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        var messages = await _repo.ListByUserAsync(userId, from, to, limit, ct);

        return new ChatHistoryResponse
        {
            Messages = messages
                .Select(m => new ChatMessageResponse
                {
                    MessageId = m.MessageId,
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    // Safety flag chỉ có trên message vừa sinh; history từ DB
                    // không lưu flag riêng — suy ra từ nội dung (Safety response không
                    // bao giờ chứa "độ tin cậy AI").
                    IsSafetyResponse = IsSafetyResponse(m.Content),
                })
                .ToList(),
        };
    }

    private async Task<IReadOnlyList<ChatTurn>> BuildHistoryForLlm(
        Guid userId,
        CancellationToken ct)
    {
        // Lấy 10 tin nhắn gần nhất làm context (không lấy quá nhiều để tránh token bloat)
        var recent = await _repo.ListByUserAsync(
            userId,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            10,
            ct);

        // Đảo ngược: cũ trước, mới sau (LLM expects chronological order)
        return recent
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatTurn(m.Role, m.Content))
            .ToList();
    }

    private static bool IsSafetyResponse(string content)
    {
        // Safety response không bao giờ chứa "độ tin cậy AI" (chỉ normal AI response mới có badge)
        return !content.Contains("độ tin cậy AI", StringComparison.OrdinalIgnoreCase);
    }
}
