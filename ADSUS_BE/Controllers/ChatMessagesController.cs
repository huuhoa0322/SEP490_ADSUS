using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// FT-39 / UC-26 — Patient AI Chatbot.
/// GB-02: AI hỗ trợ, không thay thế.
/// GB-03: KHÔNG có DELETE endpoint.
/// GB-09: Patient chỉ Mobile — endpoint này cho Patient (Mobile).
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class ChatMessagesController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatMessagesController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// POST /api/v1/me/chat/messages — Patient gửi tin nhắn cho chatbot.
    /// userId lấy từ JWT, KHÔNG từ body.
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

        try
        {
            var result = await _chatService.SendMessageAsync(userId, request, ct);
            return Ok(ApiResponse<ChatMessageResponse>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, ex.Message));
        }
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
