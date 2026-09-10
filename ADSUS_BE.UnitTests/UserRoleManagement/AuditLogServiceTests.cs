using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

/// <summary>
/// SCR-08 — nhật ký gần đây trên Dashboard. Chưa từng có test trước P12 review Feature 2
/// (28/08/2026), dù có logic clamp giới hạn cần bảo vệ.
/// </summary>
public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        _sut = new AuditLogService(_auditLogs.Object);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task GetRecentAsync_LimitOutOfRange_FallsBackToDefaultTen(int requestedLimit)
    {
        _auditLogs.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<AuditLogEntry>());

        await _sut.GetRecentAsync(requestedLimit, TestContext.Current.CancellationToken);

        _auditLogs.Verify(r => r.GetRecentAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_LimitWithinRange_PassedThroughUnchanged()
    {
        _auditLogs.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<AuditLogEntry>());

        await _sut.GetRecentAsync(50, TestContext.Current.CancellationToken);

        _auditLogs.Verify(r => r.GetRecentAsync(50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_MapsAllFieldsFromEntry()
    {
        var entry = new AuditLogEntry(
            LogId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            ActorName: "Nguyễn Văn A",
            ActorRole: "ADMIN",
            Action: "DEACTIVATE_ACCOUNT",
            Detail: "vô hiệu hoá vĩnh viễn, trạng thái trước đó ACTIVE",
            PerformedAt: DateTime.UtcNow);
        _auditLogs.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new[] { entry });

        var result = await _sut.GetRecentAsync(10, TestContext.Current.CancellationToken);

        var mapped = Assert.Single(result);
        Assert.Equal(entry.LogId, mapped.LogId);
        Assert.Equal(entry.ActorId, mapped.ActorId);
        Assert.Equal(entry.ActorName, mapped.ActorName);
        Assert.Equal(entry.ActorRole, mapped.ActorRole);
        Assert.Equal(entry.Action, mapped.Action);
        Assert.Equal(entry.Detail, mapped.Detail);
        Assert.Equal(entry.PerformedAt, mapped.PerformedAt);
    }

    [Theory]
    [InlineData(0, 0, 1, 15)]
    [InlineData(-2, -10, 1, 15)]
    [InlineData(1, 101, 1, 15)]
    [InlineData(2, 20, 2, 20)]
    public async Task GetPagedAsync_ClampsPageAndPageSize(
        int inputPage, int inputPageSize, int expectedPage, int expectedPageSize)
    {
        _auditLogs.Setup(r => r.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<AuditLogEntry>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, inputPage, inputPageSize, TestContext.Current.CancellationToken);

        _auditLogs.Verify(r => r.GetPagedAsync(
            null, null, null, null, null,
            expectedPage, expectedPageSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_CalculatesTotalPagesAndMapsAllFields()
    {
        var entry = new AuditLogEntry(
            LogId: Guid.NewGuid(),
            ActorId: Guid.NewGuid(),
            ActorName: "Dr. Admin",
            ActorRole: "ADMIN",
            Action: "REGISTER_AI_MODEL",
            Detail: "Created version 1.0",
            PerformedAt: DateTime.UtcNow);

        _auditLogs.Setup(r => r.GetPagedAsync(
            "AI", "REGISTER", "ADMIN", null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { entry }, 25));

        var result = await _sut.GetPagedAsync(
            "AI", "REGISTER", "ADMIN", null, null, 1, 10, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalItems);
        Assert.Equal(3, result.TotalPages);

        var item = Assert.Single(result.Items);
        Assert.Equal(entry.LogId, item.LogId);
        Assert.Equal(entry.ActorId, item.ActorId);
        Assert.Equal(entry.ActorName, item.ActorName);
        Assert.Equal(entry.ActorRole, item.ActorRole);
        Assert.Equal(entry.Action, item.Action);
        Assert.Equal(entry.Detail, item.Detail);
        Assert.Equal(entry.PerformedAt, item.PerformedAt);
    }
}
