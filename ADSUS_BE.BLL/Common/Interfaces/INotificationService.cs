namespace ADSUS_BE.BLL.Common.Interfaces;

/// <summary>
/// Service gửi notification (lưu DB + push FCM).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gửi notification đến một user (lưu vào DB + gửi FCM push).
    /// </summary>
    Task<Guid> SendAsync(SendNotificationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gửi notification đến nhiều users.
    /// </summary>
    Task SendBulkAsync(IEnumerable<Guid> userIds, SendNotificationRequest request, CancellationToken ct = default);
}

/// <summary>
/// Request gửi notification.
/// </summary>
public record SendNotificationRequest
{
    /// <summary>User nhận notification.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Loại notification: medication_reminder, appointment_confirmed, ...</summary>
    public string Type { get; init; } = "general";

    /// <summary>Tiêu đề notification.</summary>
    public required string Title { get; init; }

    /// <summary>Nội dung notification.</summary>
    public string? Body { get; init; }

    /// <summary>Deep link để navigate khi bấm notification (VD: /appointments/123).</summary>
    public string? DeepLink { get; init; }

    /// <summary>Metadata JSON chứa ID cần thiết để navigate.</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
