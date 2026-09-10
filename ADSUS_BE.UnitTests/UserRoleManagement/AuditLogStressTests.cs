using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

/// <summary>
/// Adversarial stress tests for Audit Log Paged Search (Milestone 1).
/// Tests boundary date ranges, page limits, injection attacks, null fields, and exception safety.
/// </summary>
public class AuditLogStressTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AuditLogRepository _repo;
    private readonly AuditLogService _service;
    private readonly AuditLogsController _controller;

    public AuditLogStressTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repo = new AuditLogRepository(_context);
        _service = new AuditLogService(_repo);
        _controller = new AuditLogsController(_service);

        // Mock ControllerContext with empty query string dictionary
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedAuditLogsAsync(int count = 5)
    {
        var actor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Admin Stress",
            Phone = "0900000001",
            Email = "admin@stress.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            Status = UserStatus.Active,
        };
        _context.Users.Add(actor);

        var baseTime = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < count; i++)
        {
            var log = new AuditLog
            {
                LogId = Guid.NewGuid(),
                ActorId = actor.UserId,
                Actor = actor,
                Action = i % 2 == 0 ? "ACCOUNT_LOCK" : "PASSWORD_RESET",
                Detail = i == 0 ? null : $"Target user ID: {Guid.NewGuid()}",
                PerformedAt = baseTime.AddDays(-i),
            };
            _context.AuditLogs.Add(log);
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetPagedAsync_FromDateGreaterThanToDate_ReturnsEmptyResultWithoutCrashing()
    {
        await SeedAuditLogsAsync(5);

        var fromDate = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);

        var (items, totalCount) = await _repo.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: null,
            fromDate: fromDate,
            toDate: toDate,
            page: 1,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);

        // Through service
        var serviceResult = await _service.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: null,
            fromDate: fromDate,
            toDate: toDate,
            page: 1,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(serviceResult.Items);
        Assert.Equal(0, serviceResult.TotalItems);
        Assert.Equal(0, serviceResult.TotalPages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task GetPagedAsync_PageLessThanOrEqualToZero_ClampedToPageOne(int invalidPage)
    {
        await SeedAuditLogsAsync(3);

        var result = await _service.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: invalidPage,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task GetPagedAsync_PageSizeLessThanOrEqualToZero_ClampedToDefault15(int invalidPageSize)
    {
        await SeedAuditLogsAsync(3);

        var result = await _service.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: invalidPageSize,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(15, result.PageSize);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_PageSizeExtreme9999_ClampedToDefault15()
    {
        await SeedAuditLogsAsync(3);

        var result = await _service.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 9999,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(15, result.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondTotalCount_ReturnsEmptyItemsSafely()
    {
        await SeedAuditLogsAsync(3);

        var result = await _service.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: 99999,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE \"AuditLogs\"; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("admin'--")]
    [InlineData("!@#$%^&*()_+-=[]{}|;':\",.<>/?")]
    [InlineData("🏥 🔒")]
    public async Task GetPagedAsync_MaliciousKeywordAndAction_HandledSafely(string maliciousPayload)
    {
        await SeedAuditLogsAsync(3);

        // In keyword
        var kwResult = await _service.GetPagedAsync(
            keyword: maliciousPayload,
            action: null,
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(kwResult);
        Assert.Empty(kwResult.Items);
        Assert.Equal(0, kwResult.TotalItems);

        // In action
        var actionResult = await _service.GetPagedAsync(
            keyword: null,
            action: maliciousPayload,
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(actionResult);
        Assert.Empty(actionResult.Items);
        Assert.Equal(0, actionResult.TotalItems);
    }

    [Theory]
    [InlineData("INVALID_ROLE")]
    [InlineData("'; DROP TABLE...")]
    [InlineData("   ")]
    public async Task GetPagedAsync_InvalidActorRole_HandledSafelyWithoutCrash(string invalidRole)
    {
        await SeedAuditLogsAsync(3);

        var result = await _service.GetPagedAsync(
            keyword: null,
            action: null,
            actorRole: invalidRole,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.TotalItems >= 0);
    }

    [Fact]
    public async Task GetPagedAsync_NullFieldsOnAuditLog_HandledWithoutNullReference()
    {
        var actor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Admin NullDetail User",
            Phone = "0900000000",
            Email = "nulldetail@test.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            Status = UserStatus.Active,
        };
        _context.Users.Add(actor);

        var log = new AuditLog
        {
            LogId = Guid.NewGuid(),
            ActorId = actor.UserId,
            Actor = actor,
            Action = "SYSTEM_EVENT",
            Detail = null,
            PerformedAt = DateTime.UtcNow,
        };
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            action: "SYSTEM_EVENT",
            actorRole: null,
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Null(result.Items[0].Detail);
        Assert.Equal("Admin NullDetail User", result.Items[0].ActorName);
    }

    [Fact]
    public async Task GetAuditLogs_Controller_BoundaryParameters_ReturnsOk()
    {
        await SeedAuditLogsAsync(3);

        var actionResult = await _controller.GetAuditLogs(
            search: "<script>",
            keyword: null,
            action: "ACCOUNT",
            role: "INVALID",
            actorRole: null,
            fromDate: new DateTime(2026, 9, 20),
            toDate: new DateTime(2026, 9, 10),
            limit: null,
            page: -5,
            pageSize: 0,
            cancellationToken: TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var envelope = Assert.IsType<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(okResult.Value);
        Assert.Equal(200, envelope.Code);
        Assert.NotNull(envelope.Data);
        Assert.Equal(1, envelope.Data.Page);
        Assert.Equal(15, envelope.Data.PageSize);
    }
}
