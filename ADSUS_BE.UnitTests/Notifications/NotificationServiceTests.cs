using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.DTOs;
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
        var result = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

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
        await _sut.SendAsync(request, TestContext.Current.CancellationToken);

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
        await _sut.SendAsync(request, TestContext.Current.CancellationToken);

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
        await _sut.SendAsync(request, TestContext.Current.CancellationToken);

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
        await _sut.SendBulkAsync(new[] { userId1, userId2 }, request, TestContext.Current.CancellationToken);

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
        var result = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert - Vẫn lưu vào DB
        Assert.NotEqual(Guid.Empty, result);
        _notificationLogRepo.Verify(r => r.CreateAsync(
            It.IsAny<NotificationLog>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-007: SendAsync - Web DeepLink Conversion to Mobile Scheme

    [Theory]
    [InlineData("https://adsus.example.com/appointments/1", "adsus://appointments/1")]
    [InlineData("https://adsus.com/prescriptions/2", "adsus://prescriptions/2")]
    [InlineData("adsus://custom/route", "adsus://custom/route")]
    [InlineData("adsus:///appointments/1", "adsus://appointments/1")]
    [InlineData("adsus:////appointments/1", "adsus://appointments/1")]
    [InlineData("adsus://reminders?intakeId=123", "adsus://reminders?intakeId=123")]
    [InlineData("adsus:///reminders?intakeId=123", "adsus://reminders?intakeId=123")]
    [InlineData("https://staging.adsus.com/appointments/1", "adsus://appointments/1")]
    [InlineData("https://adsus.com:5000/checkin/123", "adsus://checkin/123")]
    [InlineData("https://adsus.example.com:8080/checkin/123", "adsus://checkin/123")]
    [InlineData("https://user:secret@adsus.com/appointments/1", "adsus://appointments/1")]
    [InlineData("https://adsus.com", "adsus://")]
    [InlineData("https://adsus.com/", "adsus://")]
    [InlineData("https://adsus.com//appointments/1", "adsus://appointments/1")]
    [InlineData("  https://adsus.com/appointments/1  ", "adsus://appointments/1")]
    [InlineData("  adsus:///appointments/1  ", "adsus://appointments/1")]
    [InlineData("HTTPS://ADSUS.COM/appointments/1", "adsus://appointments/1")]
    [InlineData("https://Adsus.Example.Com:8080/checkin/123", "adsus://checkin/123")]
    [InlineData("https://adsus.com/reminders?intakeId=123&action=view#details", "adsus://reminders?intakeId=123&action=view#details")]
    [InlineData("https://adsus.example.com/reminders?intakeId=123#frag", "adsus://reminders?intakeId=123#frag")]
    [InlineData("https://adsus.com:5000/reminders?intakeId=123#frag", "adsus://reminders?intakeId=123#frag")]
    [InlineData("https://adsus.com/reminders?title=A%20B&val=1%2B2", "adsus://reminders?title=A%20B&val=1%2B2")]
    [InlineData("https://adsus.com?intakeId=123", "adsus://?intakeId=123")]
    [InlineData("https://adsus.com/?intakeId=123", "adsus://?intakeId=123")]
    [InlineData("https://adsus.com#details", "adsus://#details")]
    [InlineData("https://adsus.com/#details", "adsus://#details")]
    [InlineData("https://external-service.com/appointments/1", "https://external-service.com/appointments/1")]
    [InlineData("https://adsus.community/forum", "https://adsus.community/forum")]
    [InlineData("https://adsus.com.attacker.com/malware", "https://adsus.com.attacker.com/malware")]
    [InlineData("https://adsus.company.com/test", "https://adsus.company.com/test")]
    [InlineData("https://adsus.example.community/foo", "https://adsus.example.community/foo")]
    [InlineData("https://adsus.com@attacker.com/steal", "https://adsus.com@attacker.com/steal")]
    [InlineData("https://attacker-adsus.com/appointments/1", "https://attacker-adsus.com/appointments/1")]
    [InlineData("https://eviladsus.com/appointments/1", "https://eviladsus.com/appointments/1")]
    [InlineData("https://adsus.com.co/appointments/1", "https://adsus.com.co/appointments/1")]
    [InlineData("https://attacker.com/?https://adsus.com", "https://attacker.com/?https://adsus.com")]
    [InlineData("https://attacker.com/adsus.com", "https://attacker.com/adsus.com")]
    [InlineData("ftp://adsus.com/files/1", "ftp://adsus.com/files/1")]
    [InlineData("mailto:support@adsus.com", "mailto:support@adsus.com")]
    public async Task SendAsync_WebDeepLink_ConvertsToMobileScheme(string inputDeepLink, string expectedDeepLink)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "test",
            Title = "DeepLink Test",
            DeepLink = inputDeepLink
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        _pushClient.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<PushMessage>(p => p.DeepLink == expectedDeepLink),
            It.IsAny<CancellationToken>()), Times.Once);

        _realTimeService.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<NotificationMessage>(m => m.DeepLink == expectedDeepLink)), Times.Once);
    }

    [Fact]
    public async Task SendAsync_HttpWebDeepLinks_ConvertsToMobileScheme()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var scheme = "http";
        var httpExample = $"{scheme}://adsus.example.com/checkin/123";
        var httpAdsus = $"{scheme}://adsus.com/records/456";

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act & Assert 1: adsus.example.com
        var req1 = new SendNotificationRequest
        {
            UserId = userId,
            Type = "test",
            Title = "Test 1",
            DeepLink = httpExample
        };
        await _sut.SendAsync(req1, TestContext.Current.CancellationToken);
        _pushClient.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<PushMessage>(p => p.DeepLink == "adsus://checkin/123"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Act & Assert 2: adsus.com
        var req2 = new SendNotificationRequest
        {
            UserId = userId,
            Type = "test",
            Title = "Test 2",
            DeepLink = httpAdsus
        };
        await _sut.SendAsync(req2, TestContext.Current.CancellationToken);
        _pushClient.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<PushMessage>(p => p.DeepLink == "adsus://records/456"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_NullOrEmptyDeepLink_GeneratesDefaultNotificationDeepLink(string? inputDeepLink)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = "test",
            Title = "Test",
            DeepLink = inputDeepLink
        };

        _notificationLogRepo.Setup(r => r.CreateAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationLog log, CancellationToken _) => log);

        // Act
        var logId = await _sut.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var expectedDeepLink = $"adsus://notifications/{logId}";
        _pushClient.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<PushMessage>(p => p.DeepLink == expectedDeepLink),
            It.IsAny<CancellationToken>()), Times.Once);

        _realTimeService.Verify(s => s.SendToUserAsync(
            userId,
            It.Is<NotificationMessage>(m => m.DeepLink == expectedDeepLink)), Times.Once);
    }

    #endregion
}
