using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class AuditLogRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AuditLogRepository _sut;

    public AuditLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new AuditLogRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static User CreateUser(string name, string phone, UserRole role)
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = name,
            Phone = phone,
            Role = role,
            Email = $"{name.ToLower().Replace(" ", "")}@test.com",
            PasswordHash = "hash",
            Status = UserStatus.Active,
        };
    }

    private static AuditLog CreateLog(User actor, string action, string? detail, DateTime performedAt)
    {
        return new AuditLog
        {
            LogId = Guid.NewGuid(),
            ActorId = actor.UserId,
            Actor = actor,
            Action = action,
            Detail = detail,
            PerformedAt = performedAt,
        };
    }

    [Fact]
    public async Task GetPagedAsync_KeywordSearch_MatchesActorNamePhoneActionDetail()
    {
        // Arrange
        var user1 = CreateUser("Nguyen Van Admin", "0901111111", UserRole.Admin);
        var user2 = CreateUser("Tran Thi Nurse", "0902222222", UserRole.Staff);

        var log1 = CreateLog(user1, "CREATE_ACCOUNT", "Created doctor account", DateTime.UtcNow.AddMinutes(-10));
        var log2 = CreateLog(user2, "NURSE_CHECKIN", "Checked in patient with severe cough", DateTime.UtcNow.AddMinutes(-5));

        _context.Users.AddRange(user1, user2);
        _context.AuditLogs.AddRange(log1, log2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act 1: Search by actor name
        var (items1, total1) = await _sut.GetPagedAsync("Admin", null, null, null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total1);
        Assert.Equal(log1.LogId, items1[0].LogId);

        // Act 2: Search by phone
        var (items2, total2) = await _sut.GetPagedAsync("0902222", null, null, null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total2);
        Assert.Equal(log2.LogId, items2[0].LogId);

        // Act 3: Search by action
        var (items3, total3) = await _sut.GetPagedAsync("CREATE", null, null, null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total3);
        Assert.Equal(log1.LogId, items3[0].LogId);

        // Act 4: Search by detail
        var (items4, total4) = await _sut.GetPagedAsync("severe cough", null, null, null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total4);
        Assert.Equal(log2.LogId, items4[0].LogId);
    }

    [Fact]
    public async Task GetPagedAsync_ActionFilter_MatchesExactOrPrefix()
    {
        // Arrange
        var user = CreateUser("Admin User", "0900000000", UserRole.Admin);
        var log1 = CreateLog(user, "CREATE_ACCOUNT", null, DateTime.UtcNow.AddMinutes(-10));
        var log2 = CreateLog(user, "UPDATE_ACCOUNT", null, DateTime.UtcNow.AddMinutes(-5));
        var log3 = CreateLog(user, "REGISTER_AI_MODEL", null, DateTime.UtcNow);

        _context.Users.Add(user);
        _context.AuditLogs.AddRange(log1, log2, log3);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Filter by "CREATE_ACCOUNT"
        var (itemsExact, totalExact) = await _sut.GetPagedAsync(null, "CREATE_ACCOUNT", null, null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalExact);
        Assert.Equal(log1.LogId, itemsExact[0].LogId);

        // Act: Filter by prefix "REGISTER"
        var (itemsPrefix, totalPrefix) = await _sut.GetPagedAsync(null, "REGISTER", null, null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalPrefix);
        Assert.Equal(log3.LogId, itemsPrefix[0].LogId);
    }

    [Fact]
    public async Task GetPagedAsync_RoleFilter_FiltersByActorRole()
    {
        // Arrange
        var admin = CreateUser("Admin A", "0901", UserRole.Admin);
        var doctor = CreateUser("Doctor B", "0902", UserRole.Doctor);

        var log1 = CreateLog(admin, "ACT1", null, DateTime.UtcNow.AddMinutes(-10));
        var log2 = CreateLog(doctor, "ACT2", null, DateTime.UtcNow.AddMinutes(-5));

        _context.Users.AddRange(admin, doctor);
        _context.AuditLogs.AddRange(log1, log2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Filter by role "Doctor"
        var (items, total) = await _sut.GetPagedAsync(null, null, "Doctor", null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total);
        Assert.Equal(log2.LogId, items[0].LogId);
        Assert.Equal("DOCTOR", items[0].ActorRole);
    }

    [Fact]
    public async Task GetPagedAsync_DateRange_FiltersCorrectly()
    {
        // Arrange
        var user = CreateUser("Admin User", "0900", UserRole.Admin);
        var now = DateTime.UtcNow;

        var logPast = CreateLog(user, "PAST", null, now.AddDays(-5));
        var logInRange = CreateLog(user, "IN_RANGE", null, now.AddDays(-1));
        var logFuture = CreateLog(user, "FUTURE", null, now.AddDays(2));

        _context.Users.Add(user);
        _context.AuditLogs.AddRange(logPast, logInRange, logFuture);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Filter from 2 days ago to now
        var fromDate = now.AddDays(-2);
        var toDate = now;
        var (items, total) = await _sut.GetPagedAsync(null, null, null, fromDate, toDate, 1, 15, TestContext.Current.CancellationToken);

        Assert.Equal(1, total);
        Assert.Equal(logInRange.LogId, items[0].LogId);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsCorrectItemsAndTotalCount()
    {
        // Arrange
        var user = CreateUser("Admin Pager", "0999", UserRole.Admin);
        _context.Users.Add(user);

        var baseTime = DateTime.UtcNow.AddHours(-30);
        for (int i = 0; i < 25; i++)
        {
            var log = CreateLog(user, $"ACTION_{i:D2}", $"Detail {i}", baseTime.AddHours(i));
            _context.AuditLogs.Add(log);
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Page 1, Size 10
        var (itemsPage1, total1) = await _sut.GetPagedAsync(null, null, null, null, null, 1, 10, TestContext.Current.CancellationToken);
        Assert.Equal(25, total1);
        Assert.Equal(10, itemsPage1.Count);
        // Most recent first -> ACTION_24 is first
        Assert.Equal("ACTION_24", itemsPage1[0].Action);

        // Act: Page 3, Size 10 (remaining 5 items)
        var (itemsPage3, total3) = await _sut.GetPagedAsync(null, null, null, null, null, 3, 10, TestContext.Current.CancellationToken);
        Assert.Equal(25, total3);
        Assert.Equal(5, itemsPage3.Count);
    }
}
