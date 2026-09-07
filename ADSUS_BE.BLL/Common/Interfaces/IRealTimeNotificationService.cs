using ADSUS_BE.BLL.Common.DTOs;

namespace ADSUS_BE.BLL.Common.Interfaces;

public interface IRealTimeNotificationService
{
    Task SendToUserAsync(Guid userId, NotificationMessage notification);
    Task SendToAllAdminsAsync(NotificationMessage notification);
}
