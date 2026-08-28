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
///
/// Phase 2 — Intent Detection: trước khi gọi LLM, ChatDataAggregator chỉ query
/// data sources cần thiết dựa trên intent đã detect (giảm latency + token bloat).
/// </summary>
public sealed class ChatService : IChatService
{
    private const int MaxContentLength = 1000;

    private const string DefaultSystemPrompt =
        "Bạn là trợ lý sức khỏe của ADSUS. " +
        "Trả lời ngắn gọn, dựa trên kết quả khám đã được bác sĩ xác nhận. " +
        "Luôn kèm miễn trừ trách nhiệm.";

    private readonly IAiChatMessageRepository _repo;
    private readonly IPsychologyTopicFilter _psychologyFilter;
    private readonly IChatClient _chatClient;
    private readonly IIntentDetector _intentDetector;
    private readonly IChatDataAggregator _aggregator;
    private readonly ILogger<ChatService> _logger;
    private readonly string _systemPrompt;

    public ChatService(
        IAiChatMessageRepository repo,
        IPsychologyTopicFilter psychologyFilter,
        IChatClient chatClient,
        IIntentDetector intentDetector,
        IChatDataAggregator aggregator,
        ILogger<ChatService> logger,
        IOptions<AiBackendSettings> settings)
    {
        _repo = repo;
        _psychologyFilter = psychologyFilter;
        _chatClient = chatClient;
        _intentDetector = intentDetector;
        _aggregator = aggregator;
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
        IntentResult? intent = null;

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
            // Phase 2: detect intent → selective query data sources
            intent = await _intentDetector.DetectAsync(content, ct);

            // Safe: gọi LLM. Disclaimer đã hiển thị ở Flutter UI (banner cố định + badge đầu bubble),
            // nên BE không ghép DisclaimerText.General vào nữa — tránh lặp khi LLM tự thêm.
            var history = await BuildHistoryForLlm(userId, ct);
            var effectivePrompt = await BuildSystemPromptAsync(userId, intent, ct);
            var llmResponse = await _chatClient.SendMessageAsync(
                effectivePrompt, history, content, ct);
            assistantContent = llmResponse.Trim();
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
            DetectedIntent = isSafety ? null : intent?.Intent,
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

    /// <summary>
    /// Ghép patient context vào system prompt.
    /// Nếu không có hồ sơ nền, trả về prompt gốc.
    /// Phase 2: chỉ inject sections có data thực (dựa trên intent.TriggeredSources).
    /// </summary>
    private async Task<string> BuildSystemPromptAsync(Guid userId, IntentResult intent, CancellationToken ct)
    {
        var context = await _aggregator.BuildContextAsync(userId, intent, ct);

        if (context?.BasicInfo is null)
            return _systemPrompt;

        var sections = new List<string> { _systemPrompt };

        sections.Add("=== THÔNG TIN BỆNH NHÂN ===");
        sections.Add($"Họ tên: {context.BasicInfo.FullName}");
        if (context.BasicInfo.Age is not null)
            sections.Add($"Tuổi: {context.BasicInfo.Age}");

        if (context.ActivePrescriptions?.Count > 0)
        {
            sections.Add("\n=== ĐƠN THUỐC ĐANG DÙNG ===");
            foreach (var rx in context.ActivePrescriptions)
            {
                sections.Add($"Đơn ngày {rx.PrescribedDate:yyyy-MM-dd}" +
                    (rx.GeneralNote is { } n ? $" ({n})" : "") + ":");
                foreach (var item in rx.Items)
                {
                    var slots = string.Join(", ",
                        Enumerable.Range(0, (int)item.DurationDays)
                            .Select(d => item.StartDate.AddDays(d).ToString("dd/MM")));
                    sections.Add($"  - {item.MedicineName} {item.Dosage}" +
                        $" × {item.DurationDays} ngày (từ {item.StartDate:dd/MM})" +
                        (item.Instructions is { } i ? $" — {i}" : ""));
                }
            }
        }

        if (context.TodayIntakes?.Count > 0)
        {
            sections.Add("\n=== LIỀU HÔM NAY ===");
            foreach (var dose in context.TodayIntakes)
            {
                var time = dose.ScheduledTime.ToString("HH:mm");
                sections.Add($"  - {dose.MedicineName} {dose.Dosage} lúc {time} [{dose.Status}]" +
                    (dose.Instructions is { } i ? $" — {i}" : ""));
            }
        }

        if (context.UpcomingAppointments?.Count > 0)
        {
            sections.Add("\n=== LỊCH HẸN SẮP TỚI ===");
            foreach (var appt in context.UpcomingAppointments)
            {
                sections.Add($"  - {appt.SlotDate:dd/MM/yyyy} {appt.StartTime:HH:mm}–{appt.EndTime:HH:mm}" +
                    $" với Bác sĩ {appt.DoctorName}" +
                    (appt.Reason is { } r ? $" (lý do: {r})" : ""));
            }
        }

        if (context.RecentCases?.Count > 0)
        {
            sections.Add("\n=== LỊCH SỬ KHÁM GẦN ĐÂY ===");
            foreach (var c in context.RecentCases)
            {
                sections.Add($"  - {c.VisitDate:dd/MM/yyyy}: " +
                    $"{(c.FinalDiagnosis?.Length > 0 == true ? c.FinalDiagnosis : "Chưa có chẩn đoán")}" +
                    (c.DoctorConclusion?.Length > 0 == true ? $" — {c.DoctorConclusion}" : ""));
            }
        }

        if (context.Allergies?.Count > 0)
        {
            sections.Add("\n=== DỊ ỨNG ===");
            sections.Add(string.Join(", ",
                context.Allergies.Select(a => $"{a.AllergyTypeName}" +
                    (a.Note?.Length > 0 == true ? $" ({a.Note})" : ""))));
        }

        if (context.Diseases?.Count > 0)
        {
            sections.Add("\n=== BỆNH NỀN ===");
            sections.Add(string.Join(", ",
                context.Diseases.Select(d => $"{d.DiseaseName}" +
                    (d.Note?.Length > 0 == true ? $" ({d.Note})" : ""))));
        }

        if (context.RecentHealthLogs?.Count > 0)
        {
            sections.Add("\n=== NHẬT KÝ SỨC KHỎE GẦN ĐÂY ===");
            foreach (var log in context.RecentHealthLogs)
            {
                sections.Add($"  - {log.LogDate:dd/MM}: {log.Content}");
            }
        }

        return string.Join(Environment.NewLine, sections);
    }
}
