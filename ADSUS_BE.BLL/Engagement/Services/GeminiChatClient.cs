using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Gemini implementation của IChatClient — dùng Google AI Generative Language API.
/// Endpoint: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
/// </summary>
public sealed class GeminiChatClient : IChatClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeminiChatClient> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GeminiChatClient(
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiChatClient> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["OpenAi:ApiKey"] ?? string.Empty;
        _model = configuration["OpenAi:Model"] ?? "gpt-4o-mini";
        _logger.LogInformation("[DEBUG] GeminiChatClient ctor - ApiKey present: {HasKey}, Model: {Model}",
            !string.IsNullOrWhiteSpace(_apiKey), _model);
    }

    public async Task<string> SendMessageAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("GeminiChatClient activated without API key.");
            return "Trợ lý AI hiện không khả dụng. Vui lòng thử lại sau.";
        }

        var client = _httpClientFactory.CreateClient("AiBackend");
        client.DefaultRequestHeaders.Clear();
        client.Timeout = TimeSpan.FromSeconds(30);

        var request = BuildRequest(systemPrompt, history, userMessage);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await client.PostAsJsonAsync(url, request, JsonOptions, ct);
            sw.Stop();
            _logger.LogInformation("[DEBUG] Gemini response {StatusCode} in {Elapsed}ms",
                response.StatusCode, sw.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Gemini API returned {StatusCode}. Body: {Body}",
                    response.StatusCode, body);
                return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, ct);
            if (result == null)
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini returned null body. Raw: {Raw}", raw);
                return "Trợ lý AI không có phản hồi. Vui lòng thử lại sau.";
            }
            var content = result.Candidates
                .FirstOrDefault()?
                .Content?.Parts?
                .FirstOrDefault()?
                .Text;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Gemini returned empty content.");
                return "Trợ lý AI không có phản hồi. Vui lòng thử lại sau.";
            }

            return content.Trim();
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException tex)
        {
            sw.Stop();
            _logger.LogError(tex, "Gemini call TIMED OUT after {Elapsed}ms", sw.ElapsedMilliseconds);
            return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
        }
        catch (HttpRequestException hex)
        {
            sw.Stop();
            _logger.LogError(hex, "Gemini HTTP error after {Elapsed}ms", sw.ElapsedMilliseconds);
            return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Gemini call failed after {Elapsed}ms", sw.ElapsedMilliseconds);
            return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
        }
    }

    private static GeminiRequest BuildRequest(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage)
    {
        var contents = new List<GeminiContent>();

        // System prompt → inject as first user turn (Gemini doesn't have system role)
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            contents.Add(new GeminiContent
            {
                Role = "user",
                Parts = new List<GeminiPart> { new() { Text = systemPrompt } },
            });
        }

        // History
        foreach (var turn in history)
        {
            contents.Add(new GeminiContent
            {
                Role = turn.Role == ChatRole.User ? "user" : "model",
                Parts = new List<GeminiPart> { new() { Text = turn.Content } },
            });
        }

        // Current user message
        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart> { new() { Text = userMessage } },
        });

        return new GeminiRequest
        {
            Contents = contents,
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = 1024,
                Temperature = 0.7f,
            },
        };
    }

    // ── Request / Response DTOs ────────────────────────────────────────────────

    private sealed class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; init; } = new();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; init; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; init; } = new();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; init; } = 500;

        [JsonPropertyName("temperature")]
        public float Temperature { get; init; } = 0.7f;
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; init; } = new();
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; init; }
    }
}
