using System;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.DashboardReporting;

/// <summary>
/// UC-05 FT-10 — kiểm truy vấn thật của DashboardRepository qua EF Core InMemory, không mock.
///
/// DashboardServiceTests mock IDashboardRepository nên không bắt được lỗi nằm NGAY TRONG
/// câu truy vấn (ví dụ quên lọc theo status) — lớp test này bù đúng chỗ trống đó.
/// </summary>
public class DashboardRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    // ---------- GetAccountCountsAsync ----------

    [Fact]
    public async Task GetAccountCountsAsync_GroupsByRoleAndStatus_Correctly()
    {
        await using var db = CreateContext();
        db.Users.AddRange(
            BuildUser(UserRole.Doctor, UserStatus.Active),
            BuildUser(UserRole.Doctor, UserStatus.Deactivated),
            BuildUser(UserRole.Nurse, UserStatus.Active),
            BuildUser(UserRole.Patient, UserStatus.Active),
            BuildUser(UserRole.Patient, UserStatus.Active),
            BuildUser(UserRole.Admin, UserStatus.Active));
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetAccountCountsAsync(CancellationToken.None);

        Assert.Equal(6, result.Total);
        Assert.Equal(1, result.AdminCount);
        Assert.Equal(2, result.DoctorCount);
        Assert.Equal(1, result.NurseCount);
        Assert.Equal(2, result.PatientCount);
        Assert.Equal(5, result.ActiveCount);
        Assert.Equal(1, result.DeactivatedCount);
    }

    // ---------- GetActivityCountsAsync ----------

    [Fact]
    public async Task GetActivityCountsAsync_NewAccounts_OnlyCountsWithinDateRangeBothEndsInclusive()
    {
        // Cột users.created_at là mốc UTC — repository phải tự quy đổi khoảng ngày phòng
        // khám sang UTC (ClinicClock), không so trực tiếp ngày với giờ.
        await using var db = CreateContext();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);

        db.Users.AddRange(
            BuildUser(UserRole.Patient, UserStatus.Active, ClinicClock.StartOfDayUtc(from)), // đầu khoảng — tính
            BuildUser(UserRole.Patient, UserStatus.Active, ClinicClock.StartOfDayUtc(to).AddHours(12)), // giữa cuối khoảng — tính
            BuildUser(UserRole.Patient, UserStatus.Active, ClinicClock.StartOfDayUtc(from).AddSeconds(-1)), // trước 1 giây — KHÔNG tính
            BuildUser(UserRole.Patient, UserStatus.Active, ClinicClock.EndOfDayExclusiveUtc(to))); // ngay đầu ngày kế tiếp — KHÔNG tính
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(from, to, CancellationToken.None);

        Assert.Equal(2, result.NewAccounts);
    }

    [Fact]
    public async Task GetActivityCountsAsync_CaseCount_FiltersByVisitDate()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);

        db.Cases.AddRange(
            BuildCase(from),
            BuildCase(to),
            BuildCase(from.AddDays(-1)),
            BuildCase(to.AddDays(1)));
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(from, to, CancellationToken.None);

        Assert.Equal(2, result.CaseCount);
    }

    [Fact]
    public async Task GetActivityCountsAsync_Appointments_GroupsBookedAndCancelledBySlotDate()
    {
        // Lọc theo NGÀY KHÁM (Slot.SlotDate), không phải ngày đặt (Appointment.CreatedAt) —
        // xem chú thích trong DashboardRepository.
        await using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);

        var slotInRange1 = BuildSlot(doctorId, from, SlotStatus.Booked);
        var slotInRange2 = BuildSlot(doctorId, to, SlotStatus.Booked);
        var slotOutOfRange = BuildSlot(doctorId, to.AddDays(1), SlotStatus.Open);
        db.ScheduleSlots.AddRange(slotInRange1, slotInRange2, slotOutOfRange);

        db.Appointments.AddRange(
            BuildAppointment(slotInRange1.SlotId, patientId, AppointmentStatus.Booked),
            BuildAppointment(slotInRange1.SlotId, patientId, AppointmentStatus.Cancelled),
            BuildAppointment(slotInRange2.SlotId, patientId, AppointmentStatus.Cancelled),
            BuildAppointment(slotOutOfRange.SlotId, patientId, AppointmentStatus.Booked));
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(from, to, CancellationToken.None);

        Assert.Equal(1, result.AppointmentBookedCount);
        Assert.Equal(2, result.AppointmentCancelledCount);
    }

    [Fact]
    public async Task GetActivityCountsAsync_ScheduleSlotCount_OnlyCountsSlotsCurrentlyOpen()
    {
        // FR §3 (Dashboard & Reporting) yêu cầu "the count of currently Open schedule slots" —
        // không phải "mọi slot rơi vào khoảng ngày đang chọn" bất kể trạng thái.
        await using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.ScheduleSlots.AddRange(
            BuildSlot(doctorId, today, SlotStatus.Open),
            BuildSlot(doctorId, today, SlotStatus.Booked),
            BuildSlot(doctorId, today, SlotStatus.Closed));
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(today, today, CancellationToken.None);

        Assert.Equal(1, result.ScheduleSlotCount);
    }

    [Fact]
    public async Task GetActivityCountsAsync_MedicationDoses_ScheduledVsTaken_FilteredByScheduledTime()
    {
        // "Đã uống" = ConfirmedAt khác null, tính theo giờ ĐƯỢC HẸN (ScheduledTime), không
        // phải giờ xác nhận — xem chú thích trong DashboardRepository.
        await using var db = CreateContext();
        var prescriptionItemId = Guid.NewGuid();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);
        var scheduledInRange = ClinicClock.StartOfDayUtc(from).AddHours(8);
        var scheduledOutOfRange = ClinicClock.StartOfDayUtc(from).AddHours(-1);

        db.MedicationIntakeLogs.AddRange(
            new MedicationIntakeLog
            {
                IntakeId = Guid.NewGuid(), PrescriptionItemId = prescriptionItemId,
                ScheduledTime = scheduledInRange, ConfirmedAt = scheduledInRange.AddMinutes(10),
            },
            new MedicationIntakeLog
            {
                IntakeId = Guid.NewGuid(), PrescriptionItemId = prescriptionItemId,
                ScheduledTime = scheduledInRange, ConfirmedAt = null,
            },
            new MedicationIntakeLog
            {
                IntakeId = Guid.NewGuid(), PrescriptionItemId = prescriptionItemId,
                ScheduledTime = scheduledOutOfRange, ConfirmedAt = scheduledOutOfRange.AddMinutes(10),
            });
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(from, to, CancellationToken.None);

        Assert.Equal(2, result.MedicationDoseCount);
        Assert.Equal(1, result.MedicationTakenCount);
    }

    // ---------- GetDailyActivityAsync ----------

    [Fact]
    public async Task GetDailyActivityAsync_GroupsAcrossAccountsCasesAppointments_OnlyDatesWithData()
    {
        await using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var day1 = new DateOnly(2026, 7, 10);
        var day2 = new DateOnly(2026, 7, 11);

        db.Users.Add(BuildUser(UserRole.Patient, UserStatus.Active, ClinicClock.StartOfDayUtc(day1).AddHours(9)));
        db.Cases.AddRange(BuildCase(day1), BuildCase(day1));
        var slot = BuildSlot(doctorId, day2, SlotStatus.Booked);
        db.ScheduleSlots.Add(slot);
        db.Appointments.Add(BuildAppointment(slot.SlotId, patientId, AppointmentStatus.Booked));
        await db.SaveChangesAsync();

        var sut = new DashboardRepository(db);
        var result = await sut.GetDailyActivityAsync(day1, day2, CancellationToken.None);

        // Repository chỉ trả về ngày CÓ phát sinh — điền các ngày trống là việc của Service.
        Assert.Equal(2, result.Count);

        var day1Result = Assert.Single(result, d => d.Date == day1);
        Assert.Equal(1, day1Result.NewAccounts);
        Assert.Equal(2, day1Result.Cases);
        Assert.Equal(0, day1Result.Appointments);

        var day2Result = Assert.Single(result, d => d.Date == day2);
        Assert.Equal(0, day2Result.NewAccounts);
        Assert.Equal(0, day2Result.Cases);
        Assert.Equal(1, day2Result.Appointments);
    }

    // ---------- helpers ----------

    private static ScheduleSlot BuildSlot(Guid doctorId, DateOnly date, SlotStatus status) => new()
    {
        SlotId = Guid.NewGuid(),
        DoctorId = doctorId,
        SlotDate = date,
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(8, 30),
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static User BuildUser(UserRole role, UserStatus status, DateTime? createdAt = null) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = $"09{Random.Shared.Next(10000000, 99999999)}",
        FullName = "Người dùng test",
        PasswordHash = "khong-dung-toi-trong-bai-test-nay",
        Role = role,
        Status = status,
        CreatedAt = createdAt ?? DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static Case BuildCase(DateOnly visitDate) => new()
    {
        CaseId = Guid.NewGuid(),
        PatientProfileId = Guid.NewGuid(),
        DoctorId = Guid.NewGuid(),
        VisitDate = visitDate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static Appointment BuildAppointment(Guid slotId, Guid patientProfileId, AppointmentStatus status) => new()
    {
        AppointmentId = Guid.NewGuid(),
        SlotId = slotId,
        PatientProfileId = patientProfileId,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
