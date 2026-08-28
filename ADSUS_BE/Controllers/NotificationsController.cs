using System.Security.Claims;
using System.Text.Json;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationLogRepository _notificationLogRepo;

    public NotificationsController(INotificationLogRepository notificationLogRepo)
    {
        _notificationLogRepo = notificationLogRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var notifications = await _notificationLogRepo.GetByUserIdAsync(
            userId, page, pageSize, false, cancellationToken);

        var unreadCount = await _notificationLogRepo.CountUnreadAsync(userId, cancellationToken);

        var response = new NotificationListResponse
        {
            Notifications = notifications.Select(n => new NotificationDto
            {
                LogId = n.LogId,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                DeepLink = n.DeepLink,
                Metadata = DeserializeMetadata(n.Metadata),
                SentAt = n.SentAt,
                ReadAt = n.ReadAt,
                IsRead = n.ReadAt != null,
            }).ToList(),
            UnreadCount = unreadCount,
        };

        return Ok(ApiResponse<NotificationListResponse>.Ok(response));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var count = await _notificationLogRepo.CountUnreadAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { count }));
    }

    [HttpPut("{logId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid logId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        await _notificationLogRepo.MarkAsReadAsync(logId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "Notification marked as read."));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        await _notificationLogRepo.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "All notifications marked as read."));
    }

    [HttpDelete("{logId}")]
    public async Task<IActionResult> DeleteNotification(Guid logId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        await _notificationLogRepo.DeleteAsync(logId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "Notification deleted."));
    }

    private static Dictionary<string, object>? DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
        }
        catch
        {
            return null;
        }
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public class NotificationListResponse
{
    public List<NotificationDto> Notifications { get; init; } = new();
    public int UnreadCount { get; init; }
}

public class NotificationDto
{
    public Guid LogId { get; init; }

    /// <summary>Loại notification: medication_reminder, appointment_confirmed, ...</summary>
    public string Type { get; init; } = "general";

    public string Title { get; init; } = null!;
    public string? Body { get; init; }
    public string? DeepLink { get; init; }

    /// <summary>Metadata chứa ID để navigate (scheduleId, appointmentId, etc.)</summary>
    public Dictionary<string, object>? Metadata { get; init; }

    public DateTime SentAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public bool IsRead { get; init; }
}
