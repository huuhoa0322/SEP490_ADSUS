using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// OpenAI implementation của IChatClient — dùng Chat Completions API.
/// Chỉ active khi AiBackendSettings.OpenAiApiKey có giá trị.
/// Fallback: FakeChatClient (đăng ký mặc định vì user không có paid key).
/// </summary>
public sealed class OpenAiChatClient : IChatClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiChatClient> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public OpenAiChatClient(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiChatClient> logger,
        IOptions<AiBackendSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = settings.Value.OpenAiApiKey;
        _model = settings.Value.OpenAiModel;
    }

    public async Task<string> SendMessageAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("OpenAiChatClient activated without API key. Falling back to empty response.");
            return "Trợ lý AI hiện không khả dụng. Vui lòng thử lại sau.";
        }

        var client = _httpClientFactory.CreateClient("AiBackend");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        client.Timeout = TimeSpan.FromSeconds(30);

        var request = BuildRequest(systemPrompt, history, userMessage);

        try
        {
            using var response = await client.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions",
                request,
                JsonOptions,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "OpenAI API returned {StatusCode}. Body: {Body}",
                    response.StatusCode, body);
                return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>(JsonOptions, ct);
            var content = result?.Choices.FirstOrDefault()?.Message.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("OpenAI returned empty content.");
                return "Trợ lý AI không có phản hồi. Vui lòng thử lại sau.";
            }

            return content.Trim();
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI call failed for user message.");
            return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
        }
    }

    private OpenAiRequest BuildRequest(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage)
    {
        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt },
        };

        foreach (var turn in history)
        {
            messages.Add(new OpenAiMessage
            {
                Role = turn.Role == ChatRole.User ? "user" : "assistant",
                Content = turn.Content,
            });
        }

        messages.Add(new OpenAiMessage { Role = "user", Content = userMessage });

        return new OpenAiRequest
        {
            Model = _model,
            Messages = messages,
            MaxTokens = 500,
            Temperature = 0.7f,
        };
    }

    // ── Request / Response DTOs ────────────────────────────────────────────────

    private sealed class OpenAiRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = "gpt-4o-mini";

        [JsonPropertyName("messages")]
        public List<OpenAiMessage> Messages { get; init; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; } = 500;

        [JsonPropertyName("temperature")]
        public float Temperature { get; init; } = 0.7f;
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class OpenAiResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice> Choices { get; init; } = new();
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage Message { get; init; } = new();
    }
}
