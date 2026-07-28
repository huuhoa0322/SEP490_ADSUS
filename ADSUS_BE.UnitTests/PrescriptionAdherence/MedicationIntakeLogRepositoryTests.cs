using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho MedicationIntakeLogRepository. 5 case:
/// - FindByItemAndTimeAsync tìm đúng (item, time) (idempotency check cho Quartz re-fire)
/// - FindByItemAndTimeAsync returns null khi không có
/// - ListByItemAsync sắp xếp theo ScheduledTime tăng dần
/// - ListByPatientRangeAsync lọc theo range
/// - AddRangeAsync add nhiều rows cùng lúc
/// </summary>
public class MedicationIntakeLogRepositoryTests
{
    private static AdsusDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AdsusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AdsusDbContext(opts);
    }

    private static MedicationIntakeLog NewLog(Guid itemId, DateTime scheduled, DateTime? confirmed = null)
        => new()
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = itemId,
            ScheduledTime = scheduled,
            ConfirmedAt = confirmed,
        };

    [Fact]
    public async Task FindByItemAndTimeAsync_Existing_ReturnsLog()
    {
        using var db = CreateContext();
        var itemId = Guid.NewGuid();
        var scheduled = new DateTime(2026, 7, 28, 7, 0, 0, DateTimeKind.Utc);
        var log = NewLog(itemId, scheduled);
        await db.MedicationIntakeLogs.AddAsync(log);
        await db.SaveChangesAsync();

        var repo = new MedicationIntakeLogRepository(db);
        var found = await repo.FindByItemAndTimeAsync(itemId, scheduled);

        Assert.NotNull(found);
        Assert.Equal(log.IntakeId, found!.IntakeId);
    }

    [Fact]
    public async Task FindByItemAndTimeAsync_NotFound_ReturnsNull()
    {
        using var db = CreateContext();
        var repo = new MedicationIntakeLogRepository(db);

        var found = await repo.FindByItemAndTimeAsync(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Null(found);
    }

    [Fact]
    public async Task ListByItemAsync_OrdersByScheduledTimeAscending()
    {
        using var db = CreateContext();
        var itemId = Guid.NewGuid();
        await db.MedicationIntakeLogs.AddRangeAsync(
            NewLog(itemId, new DateTime(2026, 7, 28, 20, 0, 0, DateTimeKind.Utc)),
            NewLog(itemId, new DateTime(2026, 7, 28, 7, 0, 0, DateTimeKind.Utc)),
            NewLog(itemId, new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var repo = new MedicationIntakeLogRepository(db);
        var logs = await repo.ListByItemAsync(itemId);

        Assert.Equal(3, logs.Count);
        Assert.Equal(new DateTime(2026, 7, 28, 7, 0, 0, DateTimeKind.Utc), logs[0].ScheduledTime);
        Assert.Equal(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc), logs[1].ScheduledTime);
        Assert.Equal(new DateTime(2026, 7, 28, 20, 0, 0, DateTimeKind.Utc), logs[2].ScheduledTime);
    }

    [Fact]
    public async Task ListByPatientRangeAsync_ReturnsLogsInRange()
    {
        using var db = CreateContext();
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var itemId = Guid.NewGuid();
        await db.MedicationIntakeLogs.AddRangeAsync(
            NewLog(itemId, from.AddDays(-1)),     // ngoài range (trước)
            NewLog(itemId, from.AddDays(5)),      // trong range
            NewLog(itemId, to.AddDays(1)));       // ngoài range (sau)
        await db.SaveChangesAsync();

        var repo = new MedicationIntakeLogRepository(db);
        var logs = await repo.ListByPatientRangeAsync(Guid.NewGuid(), from, to);

        Assert.Single(logs);
    }

    [Fact]
    public async Task AddRangeAsync_AddsAllLogs()
    {
        using var db = CreateContext();
        var itemId = Guid.NewGuid();
        var logs = new[]
        {
            NewLog(itemId, new DateTime(2026, 7, 28, 7, 0, 0, DateTimeKind.Utc)),
            NewLog(itemId, new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)),
            NewLog(itemId, new DateTime(2026, 7, 28, 20, 0, 0, DateTimeKind.Utc)),
        };

        var repo = new MedicationIntakeLogRepository(db);
        await repo.AddRangeAsync(logs);
        await db.SaveChangesAsync();

        var fetched = await db.MedicationIntakeLogs.Where(l => l.PrescriptionItemId == itemId).ToListAsync();
        Assert.Equal(3, fetched.Count);
    }
}