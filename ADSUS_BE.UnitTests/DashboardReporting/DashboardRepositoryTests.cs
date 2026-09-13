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
            BuildUser(UserRole.Staff, UserStatus.Active),
            BuildUser(UserRole.Patient, UserStatus.Active),
            BuildUser(UserRole.Patient, UserStatus.Active),
            BuildUser(UserRole.Admin, UserStatus.Active));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new DashboardRepository(db);
        var result = await sut.GetDailyActivityAsync(day1, day2, CancellationToken.None);

        // Repository chỉ trả về ngày CÓ phát sinh — điền các ngày trống là việc của Service.
        Assert.Equal(2, result.Count);

        var day1Result = Assert.Single(result, d => d.Date == day1);
        Assert.Equal(1, day1Result.NewAccounts);
        Assert.Equal(2, day1Result.Cases);
        Assert.Equal(0, day1Result.Appointments);
        Assert.Equal(0m, day1Result.Revenue);

        var day2Result = Assert.Single(result, d => d.Date == day2);
        Assert.Equal(0, day2Result.NewAccounts);
        Assert.Equal(0, day2Result.Cases);
        Assert.Equal(1, day2Result.Appointments);
        Assert.Equal(0m, day2Result.Revenue);
    }

    [Fact]
    public async Task GetActivityCountsAsync_AiRunCount_CountsDistinctImagesWithPredictions()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);
        var fromUtc = ClinicClock.StartOfDayUtc(from);
        var toUtc = ClinicClock.EndOfDayExclusiveUtc(to);

        var imageId1 = Guid.NewGuid();
        var imageId2 = Guid.NewGuid();
        var imageIdOutOfRange = Guid.NewGuid();
        var modelVersionId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        // 2 predictions on image1 (same image = 1 run), 1 on image2 (= 1 run), 1 out of range
        db.AiPredictions.AddRange(
            BuildAiPrediction(caseId, imageId1, modelVersionId, fromUtc.AddHours(1)),
            BuildAiPrediction(caseId, imageId1, modelVersionId, fromUtc.AddHours(2)),
            BuildAiPrediction(caseId, imageId2, modelVersionId, fromUtc.AddHours(3)),
            BuildAiPrediction(caseId, imageIdOutOfRange, modelVersionId, fromUtc.AddSeconds(-1)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(from, to, CancellationToken.None);

        Assert.Equal(2, result.AiRunCount); // 2 distinct images in range
    }

    [Fact]
    public async Task GetActivityCountsAsync_AiConfirmedCount_CountsImagesWithDoctorAnnotations()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);
        var fromUtc = ClinicClock.StartOfDayUtc(from);

        var imageReviewed = Guid.NewGuid();
        var imagePending = Guid.NewGuid();
        var modelVersionId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        db.AiPredictions.AddRange(
            BuildAiPrediction(caseId, imageReviewed, modelVersionId, fromUtc.AddHours(1)),
            BuildAiPrediction(caseId, imagePending, modelVersionId, fromUtc.AddHours(2)));
        db.DoctorAnnotations.Add(BuildDoctorAnnotation(caseId, imageReviewed));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new DashboardRepository(db);
        var result = await sut.GetActivityCountsAsync(from, to, CancellationToken.None);

        Assert.Equal(2, result.AiRunCount);
        Assert.Equal(1, result.AiConfirmedCount);
        Assert.Equal(1, result.AiPendingCount);
    }

    [Fact]
    public async Task GetRevenueAsync_OnlySumsPaidInvoicesWithinDateRange()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 7, 10);
        var to = new DateOnly(2026, 7, 12);
        var fromUtc = ClinicClock.StartOfDayUtc(from);
        var toUtc = ClinicClock.EndOfDayExclusiveUtc(to);

        db.Invoices.AddRange(
            BuildInvoice(InvoiceStatus.PAID, 100_000m, PaymentMethod.CASH, fromUtc.AddHours(1)),
            BuildInvoice(InvoiceStatus.PAID, 200_000m, PaymentMethod.BANK_TRANSFER, fromUtc.AddHours(2)),
            BuildInvoice(InvoiceStatus.PAID, 50_000m, PaymentMethod.CASH, fromUtc.AddSeconds(-1)),  // out of range
            BuildInvoice(InvoiceStatus.CANCELLED, 999_999m, null, null),                             // cancelled
            BuildInvoice(InvoiceStatus.PENDING, 300_000m, null, null));                               // pending
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new DashboardRepository(db);
        var result = await sut.GetRevenueAsync(from, to, CancellationToken.None);

        Assert.Equal(300_000m, result.TotalRevenue);
        Assert.Equal(2, result.PaidInvoiceCount);
        Assert.Equal(100_000m, result.CashRevenue);
        Assert.Equal(1, result.CashCount);
        Assert.Equal(200_000m, result.BankTransferRevenue);
        Assert.Equal(1, result.BankTransferCount);
        Assert.Equal(1, result.PendingInvoiceCount);
        Assert.Equal(300_000m, result.PendingAmount);
    }

    [Fact]
    public async Task GetTopPrescribedMedicinesAsync_GroupsByMedicine_ExcludesCancelled_OrdersByCount()
    {
        await using var db = CreateContext();
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 31);

        var med1 = BuildMedicine("Paracetamol 500mg");
        var med2 = BuildMedicine("Amoxicillin 250mg");
        db.Medicines.AddRange(med1, med2);

        var rxActive = BuildPrescription(from, PrescriptionStatus.Active);
        var rxCancelled = BuildPrescription(from, PrescriptionStatus.Cancelled);
        db.Prescriptions.AddRange(rxActive, rxCancelled);

        db.PrescriptionItems.AddRange(
            BuildPrescriptionItem(rxActive.PrescriptionId, med1.MedicineId, quantityBase: 30),
            BuildPrescriptionItem(rxActive.PrescriptionId, med1.MedicineId, quantityBase: 20),
            BuildPrescriptionItem(rxActive.PrescriptionId, med2.MedicineId, quantityBase: 100),
            BuildPrescriptionItem(rxCancelled.PrescriptionId, med1.MedicineId, quantityBase: 999)); // bỏ qua
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new DashboardRepository(db);
        var result = await sut.GetTopPrescribedMedicinesAsync(from, to, 10, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Paracetamol 500mg", result[0].MedicineName); // 2 lần kê > 1 lần
        Assert.Equal(2, result[0].PrescriptionCount);
        Assert.Equal(50, result[0].TotalQuantityBase); // 30 + 20
        Assert.Equal("Amoxicillin 250mg", result[1].MedicineName);
        Assert.Equal(1, result[1].PrescriptionCount);
        Assert.Equal(100, result[1].TotalQuantityBase);
    }

    [Fact]
    public async Task GetDailyActivityAsync_IncludesRevenueByDay()
    {
        await using var db = CreateContext();
        var day1 = new DateOnly(2026, 7, 10);
        var fromUtc = ClinicClock.StartOfDayUtc(day1);

        db.Invoices.Add(BuildInvoice(InvoiceStatus.PAID, 500_000m, PaymentMethod.CASH, fromUtc.AddHours(9)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new DashboardRepository(db);
        var result = await sut.GetDailyActivityAsync(day1, day1, CancellationToken.None);

        var dayResult = Assert.Single(result);
        Assert.Equal(500_000m, dayResult.Revenue);
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

    private static AiPrediction BuildAiPrediction(Guid caseId, Guid imageId, Guid modelVersionId, DateTime createdAt) => new()
    {
        PredictionId = Guid.NewGuid(),
        CaseId = caseId,
        ImageId = imageId,
        ModelVersionId = modelVersionId,
        BboxXmin = 0,
        BboxYmin = 0,
        BboxXmax = 1,
        BboxYmax = 1,
        Confidence = 0.9m,
        CreatedAt = createdAt,
    };

    private static DoctorAnnotation BuildDoctorAnnotation(Guid caseId, Guid imageId) => new()
    {
        AnnotationId = Guid.NewGuid(),
        CaseId = caseId,
        ImageId = imageId,
        BboxXmin = 0,
        BboxYmin = 0,
        BboxXmax = 1,
        BboxYmax = 1,
        Source = "doctor_added",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static Invoice BuildInvoice(InvoiceStatus status, decimal total, PaymentMethod? method, DateTime? paidAt) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = Guid.NewGuid(),
        TotalAmount = total,
        Status = status,
        PaymentMethod = method,
        PaidAt = paidAt,
        CreatedAt = DateTime.UtcNow,
    };

    private static Medicine BuildMedicine(string name) => new()
    {
        MedicineId = Guid.NewGuid(),
        Name = name,
        CreatedAt = DateTime.UtcNow,
        LowStockThreshold = 10,
        Status = MedicineStatus.Active,
    };

    private static Prescription BuildPrescription(DateOnly date, PrescriptionStatus status) => new()
    {
        PrescriptionId = Guid.NewGuid(),
        CaseId = Guid.NewGuid(),
        DoctorId = Guid.NewGuid(),
        PrescribedDate = date,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static PrescriptionItem BuildPrescriptionItem(Guid rxId, Guid medId, int quantityBase) => new()
    {
        PrescriptionItemId = Guid.NewGuid(),
        PrescriptionId = rxId,
        MedicineId = medId,
        Dosage = "1 viên",
        DurationDays = 7,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        QuantityBase = quantityBase,
    };
}
