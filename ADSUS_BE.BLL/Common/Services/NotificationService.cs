using System.Text.Json;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.Common.Services;

/// <summary>
/// Implementation gửi notification: lưu DB + push FCM.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationLogRepository _notificationRepo;
    private readonly IPushNotificationClient _pushClient;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationLogRepository notificationRepo,
        IPushNotificationClient pushClient,
        ILogger<NotificationService> logger)
    {
        _notificationRepo = notificationRepo;
        _pushClient = pushClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> SendAsync(SendNotificationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NOTIF-SERVICE] SendAsync called. UserId={UserId}, Type={Type}, Title={Title}",
            request.UserId, request.Type, request.Title);

        var logId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // 1. Serialize metadata
        string? metadataJson = null;
        if (request.Metadata is { Count: > 0 })
        {
            metadataJson = JsonSerializer.Serialize(request.Metadata);
        }

        // 2. Save to DB (Type is stored as string)
        var log = new NotificationLog
        {
            LogId = logId,
            UserId = request.UserId,
            Type = request.Type,
            NotificationType = request.Type, // Database column notification_type
            Title = request.Title,
            Body = request.Body,
            DeepLink = request.DeepLink,
            Metadata = metadataJson,
            SentAt = now,
        };

        await SaveToDbAsync(log, ct);

        // 3. Build FCM payload
        var metadataDict = request.Metadata?
            .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "")
            ?? new Dictionary<string, string>();

        var pushMessage = new PushMessage(
            Title: request.Title,
            Body: request.Body ?? "",
            DeepLink: request.DeepLink,
            Data: metadataDict);

        // 4. Send FCM
        try
        {
            _logger.LogInformation(
                "[NOTIF-FCM] About to send FCM push. UserId={UserId}, Type={Type}",
                request.UserId, request.Type);

            var sentCount = await _pushClient.SendToUserAsync(request.UserId, pushMessage, ct);

            _logger.LogInformation(
                "[NOTIF-FCM] FCM push completed. UserId={UserId}, Type={Type}, SentCount={SentCount}",
                request.UserId, request.Type, sentCount);
        }
        catch (Exception ex)
        {
            // Log lỗi nhưng không throw — notification đã lưu DB, FCM có thể retry sau
            _logger.LogError(ex,
                "[Notification] FCM push failed for user {UserId}. Type={Type}",
                request.UserId, request.Type);
        }

        return logId;
    }

    /// <inheritdoc />
    public async Task SendBulkAsync(IEnumerable<Guid> userIds, SendNotificationRequest request, CancellationToken ct = default)
    {
        var tasks = userIds.Select(userId =>
            SendAsync(new SendNotificationRequest
            {
                UserId = userId,
                Type = request.Type,
                Title = request.Title,
                Body = request.Body,
                DeepLink = request.DeepLink,
                Metadata = request.Metadata,
            }, ct));

        await Task.WhenAll(tasks);
    }

    private async Task SaveToDbAsync(NotificationLog log, CancellationToken ct)
    {
        _logger.LogInformation(
            "[NOTIF-DB] About to save notification: UserId={UserId}, Type={Type}, Title={Title}, LogId={LogId}",
            log.UserId, log.Type, log.Title, log.LogId);

        await _notificationRepo.CreateAsync(log, ct);

        _logger.LogInformation(
            "[NOTIF-DB] Saved notification to DB: LogId={LogId}, UserId={UserId}",
            log.LogId, log.UserId);
    }
}
