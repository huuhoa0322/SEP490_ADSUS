using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.Controllers;

/// <summary>
/// FT-39 / UC-26 — Patient AI Chatbot.
/// GB-02: AI hỗ trợ, không thay thế.
/// GB-03: KHÔNG có DELETE endpoint.
/// GB-09: Patient chỉ Mobile — endpoint này cho Patient (Mobile).
/// </summary>
[ApiController]
public sealed class ChatMessagesController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IChatService _chatService;
    private readonly ILogger<ChatMessagesController>? _logger;

    public ChatMessagesController(
        IChatService chatService,
        ILogger<ChatMessagesController>? logger = null)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/v1/me/chat/messages — Patient gửi tin nhắn cho chatbot.
    /// userId lấy từ JWT, KHÔNG từ body.
    /// Hỗ trợ cả Server-Sent Events (SSE) streaming và JSON response đồng bộ (backward compatible).
    /// </summary>
    [HttpPost("api/v1/me/chat/messages")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Send(
        [FromBody] SendChatMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var content = request?.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(content))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Tin nhắn không được để trống."));
        }
        if (content.Length > 1000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Tin nhắn vượt quá 1000 ký tự."));
        }

        var acceptHeader = Request?.Headers?.Accept.ToString() ?? string.Empty;
        var isStreaming = acceptHeader.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
            || Request?.Query?.ContainsKey("stream") == true;

        if (!isStreaming)
        {
            try
            {
                var result = await _chatService.SendMessageAsync(userId, request!, ct);
                return Ok(ApiResponse<ChatMessageResponse>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest, ex.Message));
            }
        }

        // SSE Streaming path
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache, no-transform";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var streamEvent in _chatService.StreamMessageAsync(userId, content, ct))
            {
                if (ct.IsCancellationRequested) break;

                var json = JsonSerializer.Serialize(streamEvent, streamEvent.GetType(), JsonOptions);
                await Response.WriteAsync($"event: {streamEvent.EventType}\ndata: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger?.LogInformation("Client aborted chat stream for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Chat streaming failed for user {UserId}", userId);
            try
            {
                var errObj = new ChatErrorEvent("Trợ lý AI đang bận. Vui lòng thử lại sau.", "STREAM_ERROR");
                var errJson = JsonSerializer.Serialize(errObj, JsonOptions);
                await Response.WriteAsync($"event: error\ndata: {errJson}\n\n", CancellationToken.None);
                await Response.Body.FlushAsync(CancellationToken.None);
            }
            catch
            {
                // Secondary error ignored
            }
        }

        return new EmptyResult();
    }

    /// <summary>
    /// GET /api/v1/me/chat/messages?from=&amp;to=&amp;limit= — Lấy lịch sử hội thoại.
    /// from/to BẮT BUỘC (anti-pattern #6: không trả toàn bộ lịch sử không giới hạn).
    /// Default limit=50, max 200.
    /// </summary>
    [HttpGet("api/v1/me/chat/messages")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<ChatHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (from == default || to == default)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Tham số 'from' và 'to' là bắt buộc."));
        }

        if (from > to)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Tham số 'from' phải nhỏ hơn hoặc bằng 'to'."));
        }

        var result = await _chatService.GetHistoryAsync(userId, from, to, limit, ct);
        return Ok(ApiResponse<ChatHistoryResponse>.Ok(result));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
