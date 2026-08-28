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

        await _sut.GetRecentAsync(requestedLimit);

        _auditLogs.Verify(r => r.GetRecentAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_LimitWithinRange_PassedThroughUnchanged()
    {
        _auditLogs.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<AuditLogEntry>());

        await _sut.GetRecentAsync(50);

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

        var result = await _sut.GetRecentAsync(10);

        var mapped = Assert.Single(result);
        Assert.Equal(entry.LogId, mapped.LogId);
        Assert.Equal(entry.ActorId, mapped.ActorId);
        Assert.Equal(entry.ActorName, mapped.ActorName);
        Assert.Equal(entry.ActorRole, mapped.ActorRole);
        Assert.Equal(entry.Action, mapped.Action);
        Assert.Equal(entry.Detail, mapped.Detail);
        Assert.Equal(entry.PerformedAt, mapped.PerformedAt);
    }
}
