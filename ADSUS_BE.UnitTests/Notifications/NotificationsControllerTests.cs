using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Notifications;

/// <summary>
/// Unit tests cho NotificationsController.
/// Các endpoint: GET /notifications, PUT /notifications/{id}/read, DELETE /notifications/{id}
/// </summary>
public class NotificationsControllerTests
{
    private readonly Mock<INotificationLogRepository> _notificationLogRepo = new();
    private readonly NotificationsController _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public NotificationsControllerTests()
    {
        _sut = new NotificationsController(_notificationLogRepo.Object);
        SetupUserContext(_sut, _userId);
    }

    #region Helper Methods

    private static void SetupUserContext(ControllerBase controller, Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static NotificationLog CreateNotificationLog(Guid logId, Guid userId, bool isRead = false)
    {
        return new NotificationLog
        {
            LogId = logId,
            UserId = userId,
            Type = "test_notification",
            Title = "Test Notification",
            Body = "This is a test",
            SentAt = DateTime.UtcNow.AddHours(-1),
            ReadAt = isRead ? DateTime.UtcNow : null,
            DeepLink = null,
            Metadata = null,
        };
    }

    #endregion

    #region TC-001: GetNotifications - Happy Path

    [Fact]
    public async Task GetNotifications_ReturnsNotifications()
    {
        // Arrange
        var logs = new List<NotificationLog>
        {
            CreateNotificationLog(Guid.NewGuid(), _userId),
            CreateNotificationLog(Guid.NewGuid(), _userId),
        };

        _notificationLogRepo.Setup(r => r.GetByUserIdAsync(_userId, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);
        _notificationLogRepo.Setup(r => r.CountUnreadAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.GetNotifications(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<NotificationListResponse>>(okResult.Value);
        Assert.Equal(200, response.Code);
        Assert.Equal(2, response.Data!.Notifications.Count);
        Assert.Equal(2, response.Data.UnreadCount);
    }

    #endregion

    #region TC-002: GetNotifications - Empty List

    [Fact]
    public async Task GetNotifications_Empty_ReturnsEmptyList()
    {
        // Arrange
        _notificationLogRepo.Setup(r => r.GetByUserIdAsync(_userId, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog>());
        _notificationLogRepo.Setup(r => r.CountUnreadAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.GetNotifications(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<NotificationListResponse>>(okResult.Value);
        Assert.Empty(response.Data!.Notifications);
        Assert.Equal(0, response.Data.UnreadCount);
    }

    #endregion

    #region TC-003: GetNotifications - With Read Status

    [Fact]
    public async Task GetNotifications_MarksIsReadCorrectly()
    {
        // Arrange
        var unreadLog = CreateNotificationLog(Guid.NewGuid(), _userId, isRead: false);
        var readLog = CreateNotificationLog(Guid.NewGuid(), _userId, isRead: true);

        _notificationLogRepo.Setup(r => r.GetByUserIdAsync(_userId, 1, 20, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog> { unreadLog, readLog });
        _notificationLogRepo.Setup(r => r.CountUnreadAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.GetNotifications(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<NotificationListResponse>>(okResult.Value);
        var notifications = response.Data!.Notifications;
        Assert.False(notifications[0].IsRead);
        Assert.True(notifications[1].IsRead);
    }

    #endregion

    #region TC-004: GetUnreadCount

    [Fact]
    public async Task GetUnreadCount_ReturnsCount()
    {
        // Arrange
        _notificationLogRepo.Setup(r => r.CountUnreadAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _sut.GetUnreadCount(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.Equal(200, response.Code);
    }

    #endregion

    #region TC-005: MarkAsRead

    [Fact]
    public async Task MarkAsRead_CallsRepository()
    {
        // Arrange
        var logId = Guid.NewGuid();

        // Act
        var result = await _sut.MarkAsRead(logId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.Equal(200, response.Code);
        _notificationLogRepo.Verify(r => r.MarkAsReadAsync(logId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-006: MarkAllAsRead

    [Fact]
    public async Task MarkAllAsRead_CallsRepository()
    {
        // Arrange & Act
        var result = await _sut.MarkAllAsRead(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.Equal(200, response.Code);
        _notificationLogRepo.Verify(r => r.MarkAllAsReadAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-007: DeleteNotification

    [Fact]
    public async Task DeleteNotification_CallsRepository()
    {
        // Arrange
        var logId = Guid.NewGuid();

        // Act
        var result = await _sut.DeleteNotification(logId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.Equal(200, response.Code);
        _notificationLogRepo.Verify(r => r.DeleteAsync(logId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-008: Unauthorized - No User Context

    [Fact]
    public async Task GetNotifications_NoUser_ReturnsUnauthorized()
    {
        // Arrange - Empty user context
        var controller = new NotificationsController(_notificationLogRepo.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        // Act
        var result = await controller.GetNotifications(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
        Assert.Equal(401, response.Code);
    }

    #endregion

    #region TC-009: GetNotifications - Pagination

    [Fact]
    public async Task GetNotifications_WithPagination_PassesParameters()
    {
        // Arrange
        _notificationLogRepo.Setup(r => r.GetByUserIdAsync(_userId, 2, 10, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationLog>());
        _notificationLogRepo.Setup(r => r.CountUnreadAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.GetNotifications(page: 2, pageSize: 10, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _notificationLogRepo.Verify(r => r.GetByUserIdAsync(_userId, 2, 10, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
