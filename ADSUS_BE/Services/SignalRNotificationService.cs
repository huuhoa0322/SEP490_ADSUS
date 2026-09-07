using ADSUS_BE.BLL.Common.DTOs;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ADSUS_BE.Services;

public class SignalRNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendToUserAsync(Guid userId, NotificationMessage notification)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation(
                "SignalR: Sent notification {LogId} to user {UserId}",
                notification.LogId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send SignalR notification to user {UserId}", userId);
        }
    }

    public async Task SendToAllAdminsAsync(NotificationMessage notification)
    {
        try
        {
            await _hubContext.Clients
                .Group("admins")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation(
                "SignalR: Sent notification {LogId} to all admins",
                notification.LogId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send SignalR notification to admins");
        }
    }
}
