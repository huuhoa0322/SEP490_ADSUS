using ADSUS_BE.BLL.Common.DTOs;
using ADSUS_BE.Hubs;
using ADSUS_BE.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Notifications;

/// <summary>
/// Unit tests cho SignalRNotificationService.
/// </summary>
public class SignalRNotificationServiceTests
{
    private readonly Mock<IHubContext<NotificationHub>> _hubContext = new();
    private readonly Mock<IClientProxy> _clientProxy = new();
    private readonly Mock<ILogger<SignalRNotificationService>> _logger = new();
    private readonly SignalRNotificationService _sut;

    public SignalRNotificationServiceTests()
    {
        _sut = new SignalRNotificationService(_hubContext.Object, _logger.Object);
    }

    #region TC-001: SendToUserAsync - Success

    [Fact]
    public async Task SendToUserAsync_Success_SendsToCorrectGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new NotificationMessage
        {
            LogId = Guid.NewGuid(),
            Type = "test",
            Title = "Test Title",
            Body = "Test Body"
        };

        var mockClients = new Mock<IHubClients>();
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _clientProxy.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendToUserAsync(userId, notification);

        // Assert
        mockClients.Verify(c => c.Group($"user_{userId}"), Times.Once);
        _clientProxy.Verify(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object[]>(args => args.Length == 1 && args[0] == notification),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-002: SendToUserAsync - Exception Handled

    [Fact]
    public async Task SendToUserAsync_Exception_DoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new NotificationMessage
        {
            LogId = Guid.NewGuid(),
            Type = "test",
            Title = "Test",
            Body = "Test"
        };

        var mockClients = new Mock<IHubClients>();
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _clientProxy.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        // Act - Should not throw
        await _sut.SendToUserAsync(userId, notification);

        // Assert - No exception thrown, log warning should have been called
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send SignalR notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region TC-003: SendToAllAdminsAsync - Success

    [Fact]
    public async Task SendToAllAdminsAsync_Success_SendsToAdminsGroup()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            LogId = Guid.NewGuid(),
            Type = "admin_alert",
            Title = "Admin Alert",
            Body = "System alert for admins"
        };

        var mockClients = new Mock<IHubClients>();
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group("admins")).Returns(_clientProxy.Object);
        _clientProxy.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendToAllAdminsAsync(notification);

        // Assert
        mockClients.Verify(c => c.Group("admins"), Times.Once);
        _clientProxy.Verify(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object[]>(args => args.Length == 1 && args[0] == notification),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-004: SendToAllAdminsAsync - Exception Handled

    [Fact]
    public async Task SendToAllAdminsAsync_Exception_DoesNotThrow()
    {
        // Arrange
        var notification = new NotificationMessage
        {
            LogId = Guid.NewGuid(),
            Type = "admin",
            Title = "Admin",
            Body = "Test"
        };

        var mockClients = new Mock<IHubClients>();
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _clientProxy.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Admin group error"));

        // Act - Should not throw
        await _sut.SendToAllAdminsAsync(notification);

        // Assert - No exception thrown
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send SignalR notification to admins")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region TC-005: SendToUserAsync - Logs Success

    [Fact]
    public async Task SendToUserAsync_Success_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var notification = new NotificationMessage
        {
            LogId = logId,
            Type = "success_test",
            Title = "Success",
            Body = "Success message"
        };

        var mockClients = new Mock<IHubClients>();
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        _clientProxy.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendToUserAsync(userId, notification);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Sent notification {logId} to user {userId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region TC-006: SendToAllAdminsAsync - Logs Success

    [Fact]
    public async Task SendToAllAdminsAsync_Success_LogsInformation()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var notification = new NotificationMessage
        {
            LogId = logId,
            Type = "admin_test",
            Title = "Admin Test",
            Body = "Test"
        };

        var mockClients = new Mock<IHubClients>();
        _hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group("admins")).Returns(_clientProxy.Object);
        _clientProxy.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SendToAllAdminsAsync(notification);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Sent notification {logId} to all admins")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
