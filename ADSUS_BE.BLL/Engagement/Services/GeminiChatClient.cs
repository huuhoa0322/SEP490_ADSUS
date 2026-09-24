using System.IO;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
        client.Timeout = TimeSpan.FromSeconds(60);

        var request = BuildRequest(systemPrompt, history, userMessage);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
                return "Trợ lý AI đang bận. Vui lòng thử lại sau.";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var response = await client.PostAsJsonAsync(url, request, JsonOptions, ct);
                sw.Stop();
                _logger.LogInformation("[DEBUG] Gemini response {StatusCode} in {Elapsed}ms (attempt {Attempt}/{MaxAttempts})",
                    response.StatusCode, sw.ElapsedMilliseconds, attempt, maxAttempts);

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "Gemini API returned {StatusCode} on attempt {Attempt}/{MaxAttempts}. Body: {Body}",
                        statusCode, attempt, maxAttempts, body);

                    var isTransient = statusCode is 503 or 429 or 500 or 502 or 504;
                    if (isTransient && attempt < maxAttempts)
                    {
                        var delayMs = attempt * 1500;
                        _logger.LogInformation("Retrying Gemini request in {DelayMs}ms (attempt {NextAttempt}/{MaxAttempts})...",
                            delayMs, attempt + 1, maxAttempts);
                        await Task.Delay(delayMs, ct);
                        continue;
                    }

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
                sw.Stop();
                _logger.LogInformation(
                    "Gemini call cancelled by client after {Elapsed}ms (ct.IsCancellationRequested=true)",
                    sw.ElapsedMilliseconds);
                return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Gemini call transient error on attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);
                var delayMs = attempt * 1500;
                await Task.Delay(delayMs, ct);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Gemini call failed after {Elapsed}ms on final attempt.", sw.ElapsedMilliseconds);
                return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
            }
        }

        return "Trợ lý AI đang bận. Vui lòng thử lại sau.";
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("GeminiChatClient activated without API key.");
            yield return "Trợ lý AI hiện không khả dụng. Vui lòng thử lại sau.";
            yield break;
        }

        var client = _httpClientFactory.CreateClient("AiBackend");
        client.DefaultRequestHeaders.Clear();
        client.Timeout = TimeSpan.FromSeconds(60);

        var request = BuildRequest(systemPrompt, history, userMessage);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:streamGenerateContent?alt=sse&key={_apiKey}";

        HttpResponseMessage? response = null;
        string? connectionError = null;
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
                yield break;

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            connectionError = null;
            try
            {
                response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Gemini streaming request cancelled before response headers were received.");
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini streaming request failed to connect on attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);
                connectionError = "Trợ lý AI đang bận. Vui lòng thử lại sau.";
            }

            if (response != null)
            {
                if (response.IsSuccessStatusCode)
                {
                    // Success! Proceed to read stream
                    break;
                }

                var statusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini streaming API returned {StatusCode} on attempt {Attempt}/{MaxAttempts}. Body: {Body}",
                    statusCode, attempt, maxAttempts, body);

                response.Dispose();
                response = null;

                var isTransient = statusCode is 503 or 429 or 500 or 502 or 504;
                if (!isTransient || attempt >= maxAttempts)
                {
                    connectionError = "Trợ lý AI đang bận. Vui lòng thử lại sau.";
                    break;
                }
            }
            else if (attempt >= maxAttempts)
            {
                break;
            }

            var delayMs = attempt * 1500;
            _logger.LogInformation("Retrying Gemini streaming request in {DelayMs}ms (attempt {NextAttempt}/{MaxAttempts})...",
                delayMs, attempt + 1, maxAttempts);

            try
            {
                await Task.Delay(delayMs, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                yield break;
            }
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            yield return connectionError ?? "Trợ lý AI đang bận. Vui lòng thử lại sau.";
            yield break;
        }

        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            var hasYieldedAnyContent = false;
            string? streamReadError = null;
            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Gemini streaming read cancelled by client.");
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading from Gemini SSE stream.");
                    streamReadError = "Trợ lý AI đang bận. Vui lòng thử lại sau.";
                    break;
                }

                if (streamReadError != null)
                {
                    break;
                }

                if (line == null)
                {
                    break;
                }

                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var json = line.Substring(5).Trim();
                if (string.IsNullOrEmpty(json))
                    continue;

                GeminiResponse? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<GeminiResponse>(json, JsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Gemini SSE JSON chunk: {Line}", line);
                    continue;
                }

                var candidate = chunk?.Candidates?.FirstOrDefault();
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.FinishReason, "SAFETY", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Gemini response stopped due to SAFETY finishReason.");
                    yield return "Nội dung phản hồi bị giới hạn bởi tiêu chuẩn an toàn.";
                    hasYieldedAnyContent = true;
                    yield break;
                }

                var text = candidate.Content?.Parts?.FirstOrDefault()?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    hasYieldedAnyContent = true;
                    yield return text;
                }
            }

            if (streamReadError != null)
            {
                yield return streamReadError;
                yield break;
            }

            if (!hasYieldedAnyContent && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini stream completed without yielding any content.");
                yield return "Trợ lý AI không có phản hồi. Vui lòng thử lại sau.";
            }
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
                MaxOutputTokens = 2048,
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

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; init; }
    }
}
