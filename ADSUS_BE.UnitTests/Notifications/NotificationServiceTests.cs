using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Notifications;

/// <summary>
/// Unit tests cho NotificationService.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<INotificationLogRepository> _notificationLogRepo = new();
    private readonly Mock<IPushNotificationClient> _pushClient = new();
    private readonly Mock<IRealTimeNotificationService> _realTimeService = new();
    private readonly Mock<ILogger<NotificationService>> _logger = new();
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _sut = new NotificationService(
            _notificationLogRepo.Object,
            _pushClient.Object,
            _realTimeService.Object,
            _logger.Object);
    }

    #region TC-001: SendAsync - Saves to DB

    [Fact]
    public async Task SendAsync_SavesToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "test",
            Title = "Test Title",
            Body = "Test Body"
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _notificationLogRepo.Verify(r => r.CreateAsync(
            It.Is<NotificationLog>(log =>
                log.UserId == userId &&
                log.Type == "test" &&
                log.Title == "Test Title"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-002: SendAsync - Pushes Notification

    [Fact]
    public async Task SendAsync_PushesNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "medication_reminder",
            Title = "Reminder",
            Body = "Time to take your medicine"
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        await _sut.SendAsync(request);

        // Assert - Push client được gọi
        _pushClient.Verify(s => s.SendToUserAsync(
            userId,
            It.IsAny<PushMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-003: SendAsync - With DeepLink

    [Fact]
    public async Task SendAsync_IncludesDeepLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "appointment_confirmed",
            Title = "Confirmed",
            Body = "Your appointment is confirmed",
            DeepLink = "/appointments/123"
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        await _sut.SendAsync(request);

        // Assert - Deep link được truyền
        _pushClient.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<PushMessage>(p => p.DeepLink == "/appointments/123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-004: SendAsync - With Metadata

    [Fact]
    public async Task SendAsync_IncludesMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "medication_reminder",
            Title = "Reminder",
            Body = "Take your medicine",
            Metadata = new Dictionary<string, object>
            {
                ["scheduleId"] = "123",
                ["medicineName"] = "Aspirin"
            }
        };

        NotificationLog? capturedLog = null;
        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        await _sut.SendAsync(request);

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Contains("scheduleId", capturedLog!.Metadata);
    }

    #endregion

    #region TC-005: SendBulkAsync - Sends to Multiple Users

    [Fact]
    public async Task SendBulkAsync_SendsToAllUsers()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = Guid.Empty, // Bulk sends don't use this field
            Type = "broadcast",
            Title = "Broadcast",
            Body = "This is a broadcast"
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        await _sut.SendBulkAsync(new[] { userId1, userId2 }, request);

        // Assert
        _notificationLogRepo.Verify(r => r.CreateAsync(
            It.Is<NotificationLog>(log => log.UserId == userId1),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationLogRepo.Verify(r => r.CreateAsync(
            It.Is<NotificationLog>(log => log.UserId == userId2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-006: SendAsync - Push Failure Still Saves to DB

    [Fact]
    public async Task SendAsync_PushFailure_StillSavesToDb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "test",
            Title = "Test"
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);
        _pushClient.Setup(s => s.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Push failed"));

        // Act - Không throw
        var result = await _sut.SendAsync(request);

        // Assert - Vẫn lưu vào DB
        Assert.NotEqual(Guid.Empty, result);
        _notificationLogRepo.Verify(r => r.CreateAsync(
            It.IsAny<NotificationLog>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
