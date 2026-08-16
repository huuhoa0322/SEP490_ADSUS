using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho MedicationIntakeLogRepository. 8 case:
/// - FindByItemAndTimeAsync tìm đúng (item, time) (idempotency check cho Quartz re-fire)
/// - FindByItemAndTimeAsync returns null khi không có
/// - ListByItemAsync sắp xếp theo ScheduledTime tăng dần
/// - ListByPatientRangeAsync lọc theo range
/// - AddRangeAsync add nhiều rows cùng lúc
/// - ListUpcomingAsync trả về hết logs hôm nay (không filter ConfirmedAt)
/// - ListUpcomingAsync range là [00:00 hôm nay, 00:00 ngày mai) UTC
/// - ListUpcomingAsync loại trừ logs ngày khác
/// </summary>
public class MedicationIntakeLogRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
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

    // Helper: tạo full navigation chain PatientProfile → Case → Prescription → PrescriptionItem → log
    private async Task<(MedicationIntakeLogRepository repo, Guid patientProfileId)> CreateRepoWithNavChain(
        AppDbContext db,
        params MedicationIntakeLog[] logs)
    {
        var patientProfileId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var prescriptionId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        db.PatientProfiles.Add(new PatientProfile
        {
            PatientProfileId = patientProfileId,
            UserId = Guid.NewGuid(),
        });
        db.Cases.Add(new Case
        {
            CaseId = caseId,
            PatientProfileId = patientProfileId,
            DoctorId = Guid.NewGuid(),
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.Medicines.Add(new Medicine
        {
            MedicineId = medicineId,
            Name = "Thuốc test",
            CreatedAt = DateTime.UtcNow,
        });
        db.Prescriptions.Add(new Prescription
        {
            PrescriptionId = prescriptionId,
            CaseId = caseId,
            DoctorId = Guid.NewGuid(),
            PrescribedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.PrescriptionItems.Add(new PrescriptionItem
        {
            PrescriptionItemId = itemId,
            PrescriptionId = prescriptionId,
            MedicineId = medicineId,
            Dosage = "1 viên",
            DurationDays = 7,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        foreach (var log in logs)
        {
            log.PrescriptionItemId = itemId;
            db.MedicationIntakeLogs.Add(log);
        }
        await db.SaveChangesAsync();

        return (new MedicationIntakeLogRepository(db), patientProfileId);
    }

    [Fact]
    public async Task ListUpcomingAsync_ReturnsAllTodayIncludingTaken()
    {
        using var db = CreateContext();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var todayLog1 = NewLog(Guid.Empty, today.AddHours(8), DateTime.UtcNow); // TAKEN
        var todayLog2 = NewLog(Guid.Empty, today.AddHours(13), null);           // PENDING
        var tomorrowLog = NewLog(Guid.Empty, tomorrow.AddHours(8), null);       // ngày mai

        var (repo, patientProfileId) = await CreateRepoWithNavChain(db, todayLog1, todayLog2, tomorrowLog);

        var result = await repo.ListUpcomingAsync(patientProfileId);

        Assert.Equal(2, result.Count);
        Assert.All(result, l => Assert.Equal(today, l.ScheduledTime.Date));
    }

    [Fact]
    public async Task ListUpcomingAsync_ExcludesYesterday()
    {
        using var db = CreateContext();
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var yesterdayLog = NewLog(Guid.Empty, yesterday.AddHours(8), null);
        var todayLog = NewLog(Guid.Empty, today.AddHours(8), null);

        var (repo, patientProfileId) = await CreateRepoWithNavChain(db, yesterdayLog, todayLog);

        var result = await repo.ListUpcomingAsync(patientProfileId);

        Assert.Single(result);
        Assert.Equal(today.AddHours(8), result[0].ScheduledTime);
    }

    [Fact]
    public async Task ListUpcomingAsync_ExcludesTomorrow()
    {
        using var db = CreateContext();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var todayLog = NewLog(Guid.Empty, today.AddHours(8), null);
        var tomorrowLog = NewLog(Guid.Empty, tomorrow.AddHours(8), null);

        var (repo, patientProfileId) = await CreateRepoWithNavChain(db, todayLog, tomorrowLog);

        var result = await repo.ListUpcomingAsync(patientProfileId);

        Assert.Single(result);
        Assert.Equal(today.AddHours(8), result[0].ScheduledTime);
    }

    [Fact]
    public async Task GetIntakeStatsByPrescriptionAsync_TwoItemsMixed_ReturnsCorrectStats()
    {
        // Arrange: create full chain → 1 prescription with 2 items → known TAKEN/PENDING mix
        using var db = CreateContext();
        var patientProfileId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var prescriptionId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        db.PatientProfiles.Add(new PatientProfile { PatientProfileId = patientProfileId, UserId = Guid.NewGuid() });
        db.Cases.Add(new Case { CaseId = caseId, PatientProfileId = patientProfileId, DoctorId = Guid.NewGuid(), VisitDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Thuốc A", CreatedAt = DateTime.UtcNow });
        db.Prescriptions.Add(new Prescription { PrescriptionId = prescriptionId, CaseId = caseId, DoctorId = Guid.NewGuid(), PrescribedDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.PrescriptionItems.Add(new PrescriptionItem { PrescriptionItemId = item1Id, PrescriptionId = prescriptionId, MedicineId = medicineId, Dosage = "1 viên", DurationDays = 3, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.PrescriptionItems.Add(new PrescriptionItem { PrescriptionItemId = item2Id, PrescriptionId = prescriptionId, MedicineId = medicineId, Dosage = "2 viên", DurationDays = 3, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });

        // Item 1: 2 TAKEN + 1 PENDING = 3 total → 66.7%
        var log1Taken1 = NewLog(item1Id, new DateTime(2026, 8, 1, 7, 0, 0, DateTimeKind.Utc));
        log1Taken1.Status = IntakeStatus.Taken;
        log1Taken1.ConfirmedAt = DateTime.UtcNow;
        var log1Taken2 = NewLog(item1Id, new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        log1Taken2.Status = IntakeStatus.Taken;
        log1Taken2.ConfirmedAt = DateTime.UtcNow;
        var log1Pending = NewLog(item1Id, new DateTime(2026, 8, 1, 19, 0, 0, DateTimeKind.Utc));
        log1Pending.Status = IntakeStatus.Pending;

        // Item 2: 1 TAKEN + 0 PENDING = 1 total → 100%
        var log2Taken = NewLog(item2Id, new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
        log2Taken.Status = IntakeStatus.Taken;
        log2Taken.ConfirmedAt = DateTime.UtcNow;

        await db.MedicationIntakeLogs.AddRangeAsync(new[] { log1Taken1, log1Taken2, log1Pending, log2Taken });
        await db.SaveChangesAsync();

        var repo = new MedicationIntakeLogRepository(db);

        // Act
        var stats = await repo.GetIntakeStatsByPrescriptionAsync(new[] { item1Id, item2Id }, CancellationToken.None);

        // Assert
        Assert.Equal(2, stats.Count);

        Assert.Equal(3, stats[item1Id].TotalDoses);
        Assert.Equal(2, stats[item1Id].TakenDoses);
        Assert.Equal(1, stats[item1Id].PendingDoses);
        Assert.Equal(66.7, stats[item1Id].AdherencePercent, 1);

        Assert.Equal(1, stats[item2Id].TotalDoses);
        Assert.Equal(1, stats[item2Id].TakenDoses);
        Assert.Equal(0, stats[item2Id].PendingDoses);
        Assert.Equal(100.0, stats[item2Id].AdherencePercent, 1);
    }

    [Fact]
    public async Task GetIntakeStatsByPrescriptionAsync_EmptyList_ReturnsEmptyDictionary()
    {
        using var db = CreateContext();
        var repo = new MedicationIntakeLogRepository(db);

        var stats = await repo.GetIntakeStatsByPrescriptionAsync(Array.Empty<Guid>(), CancellationToken.None);

        Assert.Empty(stats);
    }

    [Fact]
    public async Task GetIntakeStatsByPrescriptionAsync_NoLogs_ReturnsZeroStats()
    {
        using var db = CreateContext();
        var patientProfileId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var prescriptionId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        db.PatientProfiles.Add(new PatientProfile { PatientProfileId = patientProfileId, UserId = Guid.NewGuid() });
        db.Cases.Add(new Case { CaseId = caseId, PatientProfileId = patientProfileId, DoctorId = Guid.NewGuid(), VisitDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Thuốc A", CreatedAt = DateTime.UtcNow });
        db.Prescriptions.Add(new Prescription { PrescriptionId = prescriptionId, CaseId = caseId, DoctorId = Guid.NewGuid(), PrescribedDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.PrescriptionItems.Add(new PrescriptionItem { PrescriptionItemId = itemId, PrescriptionId = prescriptionId, MedicineId = medicineId, Dosage = "1 viên", DurationDays = 3, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.SaveChangesAsync();

        var repo = new MedicationIntakeLogRepository(db);

        var stats = await repo.GetIntakeStatsByPrescriptionAsync(new[] { itemId }, CancellationToken.None);

        Assert.Single(stats);
        Assert.Equal(0, stats[itemId].TotalDoses);
        Assert.Equal(0, stats[itemId].TakenDoses);
        Assert.Equal(0, stats[itemId].PendingDoses);
        Assert.Equal(0.0, stats[itemId].AdherencePercent);
    }
}