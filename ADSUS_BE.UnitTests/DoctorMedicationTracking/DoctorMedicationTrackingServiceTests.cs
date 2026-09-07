using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;
using ADSUS_BE.BLL.DoctorMedicationTracking.Interfaces;
using ADSUS_BE.BLL.DoctorMedicationTracking.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.PrescriptionAdherence;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.DoctorMedicationTracking;

public class DoctorMedicationTrackingServiceTests
{
    private static readonly DateTime _nowUtc = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly _today = DateOnly.FromDateTime(_nowUtc);

    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static DoctorMedicationTrackingService CreateService(
        AppDbContext db,
        INotificationService? notificationService = null)
    {
        return new DoctorMedicationTrackingService(
            db,
            new PrescriptionRepository(db),
            new MedicationIntakeLogRepository(db),
            new PatientProfileRepository(db),
            notificationService ?? Mock.Of<INotificationService>(),
            Mock.Of<ILogger<DoctorMedicationTrackingService>>());
    }

    private static User NewUser(Guid id, string fullName, UserRole role)
        => new()
        {
            UserId = id,
            Email = $"{id}@test.com",
            PasswordHash = "stub",
            FullName = fullName,
            Phone = "0000000000",
            Role = role,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static PatientProfile NewProfile(Guid id, Guid userId, User user)
        => new()
        {
            PatientProfileId = id,
            UserId = userId,
            User = user,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static Case NewCase(Guid id, Guid patientProfileId, Guid doctorId, User doctor)
        => new()
        {
            CaseId = id,
            PatientProfileId = patientProfileId,
            DoctorId = doctorId,
            Doctor = doctor,
            VisitDate = _today.AddDays(-5),
            Status = CaseStatus.End,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow,
        };

    private static Prescription NewPrescription(Guid id, Guid caseId, Guid doctorId, Case caseEntity, User doctor)
        => new()
        {
            PrescriptionId = id,
            CaseId = caseId,
            DoctorId = doctorId,
            Doctor = doctor,
            PrescribedDate = _today.AddDays(-4),
            Status = PrescriptionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Case = caseEntity,
            PrescriptionItems = new List<PrescriptionItem>(),
        };

    private static PrescriptionItem NewItem(Guid id, Guid prescriptionId, Prescription prescription, Medicine med)
        => new()
        {
            PrescriptionItemId = id,
            PrescriptionId = prescriptionId,
            Prescription = prescription,
            MedicineId = med.MedicineId,
            Medicine = med,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-3),
            ScheduleSlots = new[] { ReminderSlot.Morning, ReminderSlot.Evening },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };

    // Variant: StartDate = _today - 2, DurationDays = 3 → endDate = _today (còn hiệu lực).
    // Dùng cho GetPatientListAsync sau khi áp dụng endDate filter.
    private static PrescriptionItem NewActiveItem(Guid id, Guid prescriptionId, Prescription prescription, Medicine med)
        => new()
        {
            PrescriptionItemId = id,
            PrescriptionId = prescriptionId,
            Prescription = prescription,
            MedicineId = med.MedicineId,
            Medicine = med,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-2),
            ScheduleSlots = new[] { ReminderSlot.Morning, ReminderSlot.Evening },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };

    private static MedicationIntakeLog NewLog(
        Guid id, Guid itemId, PrescriptionItem item, DateTime scheduledUtc, DateTime? confirmedUtc)
    {
        var scheduled = scheduledUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(scheduledUtc, DateTimeKind.Utc)
            : scheduledUtc;
        DateTime? confirmed = null;
        if (confirmedUtc.HasValue)
        {
            confirmed = confirmedUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(confirmedUtc.Value, DateTimeKind.Utc)
                : confirmedUtc.Value;
        }

        return new MedicationIntakeLog
        {
            IntakeId = id,
            PrescriptionItemId = itemId,
            PrescriptionItem = item,
            ScheduledTime = scheduled,
            ConfirmedAt = confirmed,
        };
    }

    #region GetPatientListAsync tests

    [Fact]
    public async Task GetPatientListAsync_NoPrescriptions_ReturnsEmptyList()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var doctorId = Guid.NewGuid();

        var result = await service.GetPatientListAsync(doctorId, null, null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Empty(result.Patients);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPatientListAsync_ReturnsPatientsWithActivePrescriptionsOnly()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        // Patient A — Active prescription (còn hiệu lực)
        var patientUserA = NewUser(Guid.NewGuid(), "Nguyễn Văn A", UserRole.Patient);
        var profileA = NewProfile(Guid.NewGuid(), patientUserA.UserId, patientUserA);
        var caseA = NewCase(Guid.NewGuid(), profileA.PatientProfileId, doctorId, doctorUser);
        var prescriptionA = NewPrescription(Guid.NewGuid(), caseA.CaseId, doctorId, caseA, doctorUser);
        var medA = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", CreatedAt = DateTime.UtcNow };
        var itemA = NewActiveItem(Guid.NewGuid(), prescriptionA.PrescriptionId, prescriptionA, medA);
        prescriptionA.PrescriptionItems.Add(itemA);

        // Patient B — Completed prescription (should be excluded)
        var patientUserB = NewUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profileB = NewProfile(Guid.NewGuid(), patientUserB.UserId, patientUserB);
        var caseB = NewCase(Guid.NewGuid(), profileB.PatientProfileId, doctorId, doctorUser);
        var prescriptionB = NewPrescription(Guid.NewGuid(), caseB.CaseId, doctorId, caseB, doctorUser);
        prescriptionB.Status = PrescriptionStatus.Completed;
        var medB = new Medicine { MedicineId = Guid.NewGuid(), Name = "Amoxicillin", CreatedAt = DateTime.UtcNow };
        var itemB = NewActiveItem(Guid.NewGuid(), prescriptionB.PrescriptionId, prescriptionB, medB);
        prescriptionB.PrescriptionItems.Add(itemB);

        db.Users.AddRange(doctorUser, patientUserA, patientUserB);
        db.PatientProfiles.AddRange(profileA, profileB);
        db.Cases.AddRange(caseA, caseB);
        db.Medicines.AddRange(medA, medB);
        db.Prescriptions.AddRange(prescriptionA, prescriptionB);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientListAsync(doctorId, null, null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Single(result.Patients);
        Assert.Equal("Nguyễn Văn A", result.Patients[0].PatientName);
    }

    [Fact]
    public async Task GetPatientListAsync_FiltersBySearchName()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUserA = NewUser(Guid.NewGuid(), "Nguyễn Văn A", UserRole.Patient);
        var profileA = NewProfile(Guid.NewGuid(), patientUserA.UserId, patientUserA);
        var caseA = NewCase(Guid.NewGuid(), profileA.PatientProfileId, doctorId, doctorUser);
        var prescriptionA = NewPrescription(Guid.NewGuid(), caseA.CaseId, doctorId, caseA, doctorUser);
        var medA = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", CreatedAt = DateTime.UtcNow };
        var itemA = NewActiveItem(Guid.NewGuid(), prescriptionA.PrescriptionId, prescriptionA, medA);
        prescriptionA.PrescriptionItems.Add(itemA);

        var patientUserB = NewUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profileB = NewProfile(Guid.NewGuid(), patientUserB.UserId, patientUserB);
        var caseB = NewCase(Guid.NewGuid(), profileB.PatientProfileId, doctorId, doctorUser);
        var prescriptionB = NewPrescription(Guid.NewGuid(), caseB.CaseId, doctorId, caseB, doctorUser);
        var medB = new Medicine { MedicineId = Guid.NewGuid(), Name = "Amoxicillin", CreatedAt = DateTime.UtcNow };
        var itemB = NewActiveItem(Guid.NewGuid(), prescriptionB.PrescriptionId, prescriptionB, medB);
        prescriptionB.PrescriptionItems.Add(itemB);

        db.Users.AddRange(doctorUser, patientUserA, patientUserB);
        db.PatientProfiles.AddRange(profileA, profileB);
        db.Cases.AddRange(caseA, caseB);
        db.Medicines.AddRange(medA, medB);
        db.Prescriptions.AddRange(prescriptionA, prescriptionB);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientListAsync(doctorId, "Trần", null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Single(result.Patients);
        Assert.Equal("Trần Thị B", result.Patients[0].PatientName);
    }

    [Fact]
    public async Task GetPatientListAsync_SortsByPatientNameAZ()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var names = new[] { "Zara", "Alice", "Bob" };
        var profiles = new List<PatientProfile>();

        foreach (var name in names)
        {
            var u = NewUser(Guid.NewGuid(), name, UserRole.Patient);
            var p = NewProfile(Guid.NewGuid(), u.UserId, u);
            var c = NewCase(Guid.NewGuid(), p.PatientProfileId, doctorId, doctorUser);
            var rx = NewPrescription(Guid.NewGuid(), c.CaseId, doctorId, c, doctorUser);
            var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "X", CreatedAt = DateTime.UtcNow };
            var item = NewActiveItem(Guid.NewGuid(), rx.PrescriptionId, rx, med);
            rx.PrescriptionItems.Add(item);

            db.Users.Add(u);
            db.PatientProfiles.Add(p);
            db.Cases.Add(c);
            db.Medicines.Add(med);
            db.Prescriptions.Add(rx);
            profiles.Add(p);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var result = await service.GetPatientListAsync(doctorId, null, null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Patients.Count);
        Assert.Equal("Alice", result.Patients[0].PatientName);
        Assert.Equal("Bob", result.Patients[1].PatientName);
        Assert.Equal("Zara", result.Patients[2].PatientName);
    }

    [Fact]
    public async Task GetPatientListAsync_CalculatesTodayAdherenceCorrectly()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Nguyễn Văn A", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);
        var prescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", CreatedAt = DateTime.UtcNow };
        var item = NewActiveItem(Guid.NewGuid(), prescription.PrescriptionId, prescription, med);
        prescription.PrescriptionItems.Add(item);

        // Today: 2 doses, 1 taken (evening=12:00 is OVERTIME since now=10:00)
        var todayMorning = _today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var todayEvening = _today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var takenLog = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayMorning, _nowUtc.AddHours(-1));
        var pendingLog = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayEvening, null);
        item.MedicationIntakeLogs.Add(takenLog);
        item.MedicationIntakeLogs.Add(pendingLog);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientListAsync(doctorId, null, null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Single(result.Patients);
        var patient = result.Patients[0];
        Assert.Equal(1, patient.TodayTaken);
        Assert.Equal(2, patient.TodayTotal);
        Assert.Equal(50m, patient.TodayAdherencePercent);
        Assert.Equal("warning", patient.AdherenceLevel);
        // 12:00 > 10:00 (now), so evening dose is PENDING not OVERTIME → HasOverdueToday = false
        Assert.False(patient.HasOverdueToday);
    }

    [Fact]
    public async Task GetPatientListAsync_ExcludesExpiredPrescriptionsFromActiveCount()
    {
        // Bệnh nhân có 2 đơn Active: 1 còn hiệu lực (endDate = _today), 1 đã hết hạn (endDate = _today - 1).
        // Sau fix: chỉ đếm 1, không hiển thị HasOverdueToday (vì dose đã qua của đơn expired không tính).
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Nguyễn Văn E", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);

        // Đơn 1: còn hiệu lực — StartDate = _today - 2, DurationDays = 3 → endDate = _today
        var activePrescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var activeMed = new Medicine { MedicineId = Guid.NewGuid(), Name = "Active Med", CreatedAt = DateTime.UtcNow };
        var activeItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = activePrescription.PrescriptionId,
            Prescription = activePrescription,
            MedicineId = activeMed.MedicineId,
            Medicine = activeMed,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-2),
            ScheduleSlots = new[] { ReminderSlot.Morning },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };
        activePrescription.PrescriptionItems.Add(activeItem);
        // Thêm 1 dose hôm nay đã uống để test không về 0/0
        var todayMorning = _today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var takenLog = NewLog(Guid.NewGuid(), activeItem.PrescriptionItemId, activeItem, todayMorning, _nowUtc.AddHours(-1));
        activeItem.MedicationIntakeLogs.Add(takenLog);

        // Đơn 2: đã hết hạn — StartDate = _today - 5, DurationDays = 3 → endDate = _today - 3 (< today)
        var expiredPrescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var expiredMed = new Medicine { MedicineId = Guid.NewGuid(), Name = "Expired Med", CreatedAt = DateTime.UtcNow };
        var expiredItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = expiredPrescription.PrescriptionId,
            Prescription = expiredPrescription,
            MedicineId = expiredMed.MedicineId,
            Medicine = expiredMed,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-5),
            ScheduleSlots = new[] { ReminderSlot.Morning },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };
        expiredPrescription.PrescriptionItems.Add(expiredItem);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.AddRange(activeMed, expiredMed);
        db.Prescriptions.AddRange(activePrescription, expiredPrescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientListAsync(doctorId, null, null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Single(result.Patients);
        var patient = result.Patients[0];
        Assert.Equal(1, patient.ActivePrescriptionCount); // chỉ đếm đơn còn hiệu lực
        Assert.Equal(1, patient.TodayTotal);
        Assert.Equal(1, patient.TodayTaken);
    }

    [Fact]
    public async Task GetPatientListAsync_HidesPatientWhenAllPrescriptionsExpired()
    {
        // Bệnh nhân chỉ có đơn đã hết hạn (Status = Active nhưng endDate < today).
        // Mong đợi: KHÔNG hiển thị trong danh sách.
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Nguyễn Văn F", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);

        var expiredPrescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var expiredMed = new Medicine { MedicineId = Guid.NewGuid(), Name = "Expired Med", CreatedAt = DateTime.UtcNow };
        var expiredItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = expiredPrescription.PrescriptionId,
            Prescription = expiredPrescription,
            MedicineId = expiredMed.MedicineId,
            Medicine = expiredMed,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-10), // endDate = _today - 8 (hết hạn từ lâu)
            ScheduleSlots = new[] { ReminderSlot.Morning },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };
        expiredPrescription.PrescriptionItems.Add(expiredItem);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(expiredMed);
        db.Prescriptions.Add(expiredPrescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientListAsync(doctorId, null, null, null, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Empty(result.Patients);
        Assert.Equal(0, result.TotalCount);
    }

    #endregion

    #region GetPatientDetailAsync tests

    [Fact]
    public async Task GetPatientDetailAsync_PatientNotFound_ThrowsResourceNotFound()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.GetPatientDetailAsync(Guid.NewGuid(), Guid.NewGuid(), _nowUtc, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPatientDetailAsync_ReturnsPrescriptionCardsWithTodayDoses()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Nguyễn Văn B", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);
        var prescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Amoxicillin", CreatedAt = DateTime.UtcNow };
        // StartDate = _today - 2, DurationDays = 3 → endDate = _today (còn trong vòng)
        var item = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = prescription.PrescriptionId,
            Prescription = prescription,
            MedicineId = med.MedicineId,
            Medicine = med,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-2),
            ScheduleSlots = new[] { ReminderSlot.Morning, ReminderSlot.Evening },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };
        prescription.PrescriptionItems.Add(item);

        var todayMorning = _today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var todayAfternoon = _today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var takenLog = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayMorning, _nowUtc.AddHours(-1));
        var overtimeLog = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayAfternoon, null);
        item.MedicationIntakeLogs.Add(takenLog);
        item.MedicationIntakeLogs.Add(overtimeLog);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientDetailAsync(doctorId, profile.PatientProfileId, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Equal("Nguyễn Văn B", result.PatientName);
        Assert.Single(result.Prescriptions);
        var card = result.Prescriptions[0];
        Assert.Equal("Amoxicillin", card.TodayDoses[0].MedicineName);
        Assert.Equal("TAKEN", card.TodayDoses[0].Status);
        Assert.Equal("PENDING", card.TodayDoses[1].Status);
    }

    [Fact]
    public async Task GetPatientDetailAsync_ExcludesExpiredPrescriptions()
    {
        // Đơn có StartDate = _today - 3, DurationDays = 3
        // → endDate = _today - 3 + 3 - 1 = _today - 1 (< today) → bị loại
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Nguyễn Văn C", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);

        var expiredPrescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Expired Med", CreatedAt = DateTime.UtcNow };
        var expiredItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = expiredPrescription.PrescriptionId,
            Prescription = expiredPrescription,
            MedicineId = med.MedicineId,
            Medicine = med,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-3), // endDate = _today - 1 → quá hạn
            ScheduleSlots = new[] { ReminderSlot.Morning },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };
        expiredPrescription.PrescriptionItems.Add(expiredItem);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(expiredPrescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientDetailAsync(doctorId, profile.PatientProfileId, _nowUtc, TestContext.Current.CancellationToken);

        // Đơn hết hạn không xuất hiện
        Assert.Empty(result.Prescriptions);
    }

    [Fact]
    public async Task GetPatientDetailAsync_IncludesPrescriptionEndingToday()
    {
        // Đơn có StartDate = _today - 2, DurationDays = 3
        // → endDate = _today - 2 + 3 - 1 = _today (hôm nay) → >= today, còn hiệu lực
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Nguyễn Văn D", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);

        var activePrescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Active Med", CreatedAt = DateTime.UtcNow };
        var activeItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = activePrescription.PrescriptionId,
            Prescription = activePrescription,
            MedicineId = med.MedicineId,
            Medicine = med,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = _today.AddDays(-2),
            ScheduleSlots = new[] { ReminderSlot.Morning },
            MedicationIntakeLogs = new List<MedicationIntakeLog>(),
        };
        activePrescription.PrescriptionItems.Add(activeItem);

        var todayMorning = _today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var takenLog = NewLog(Guid.NewGuid(), activeItem.PrescriptionItemId, activeItem, todayMorning, _nowUtc.AddHours(-1));
        activeItem.MedicationIntakeLogs.Add(takenLog);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(activePrescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.GetPatientDetailAsync(doctorId, profile.PatientProfileId, _nowUtc, TestContext.Current.CancellationToken);

        Assert.Single(result.Prescriptions);
        Assert.Equal("Active Med", result.Prescriptions[0].TodayDoses[0].MedicineName);
    }

    #endregion

    #region SendRemindersAsync tests

    [Fact]
    public async Task SendRemindersAsync_PrescriptionNotFound_ThrowsResourceNotFound()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.SendRemindersAsync(Guid.NewGuid(), Guid.NewGuid(),
                new RemindRequest(Guid.NewGuid()), _nowUtc, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendRemindersAsync_SendsNotificationPerOverdueDose()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Bệnh nhân Test", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);
        var prescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", CreatedAt = DateTime.UtcNow };
        var item = NewItem(Guid.NewGuid(), prescription.PrescriptionId, prescription, med);
        prescription.PrescriptionItems.Add(item);

        var todayMorning = _today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var log1 = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayMorning, null);
        var log2 = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayMorning.AddHours(2), null);
        item.MedicationIntakeLogs.Add(log1);
        item.MedicationIntakeLogs.Add(log2);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notifService = new Mock<INotificationService>();
        notifService
            .Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var service = CreateService(db, notifService.Object);

        var result = await service.SendRemindersAsync(doctorId, profile.PatientProfileId,
            new RemindRequest(prescription.PrescriptionId), _nowUtc, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.SentCount);
        notifService.Verify(
            n => n.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.Type == "medication_reminder" &&
                r.UserId == patientUser.UserId),
            It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendRemindersAsync_NoOverdueDoses_ReturnsZero()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Bệnh nhân Test", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, doctorId, doctorUser);
        var prescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, doctorId, caseEntity, doctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", CreatedAt = DateTime.UtcNow };
        var item = NewItem(Guid.NewGuid(), prescription.PrescriptionId, prescription, med);
        prescription.PrescriptionItems.Add(item);

        // All doses taken
        var todayMorning = _today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var takenLog = NewLog(Guid.NewGuid(), item.PrescriptionItemId, item, todayMorning, _nowUtc.AddHours(-1));
        item.MedicationIntakeLogs.Add(takenLog);

        db.Users.AddRange(doctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        var result = await service.SendRemindersAsync(doctorId, profile.PatientProfileId,
            new RemindRequest(prescription.PrescriptionId), _nowUtc, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.SentCount);
    }

    [Fact]
    public async Task SendRemindersAsync_DoesNotSendForOtherDoctorPrescription()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var doctorUser = NewUser(doctorId, "Dr. Test", UserRole.Doctor);
        var otherDoctorId = Guid.NewGuid();
        var otherDoctorUser = NewUser(otherDoctorId, "Dr. Other", UserRole.Doctor);

        var patientUser = NewUser(Guid.NewGuid(), "Bệnh nhân Test", UserRole.Patient);
        var profile = NewProfile(Guid.NewGuid(), patientUser.UserId, patientUser);
        var caseEntity = NewCase(Guid.NewGuid(), profile.PatientProfileId, otherDoctorId, otherDoctorUser);
        var prescription = NewPrescription(Guid.NewGuid(), caseEntity.CaseId, otherDoctorId, caseEntity, otherDoctorUser);
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", CreatedAt = DateTime.UtcNow };
        var item = NewItem(Guid.NewGuid(), prescription.PrescriptionId, prescription, med);
        prescription.PrescriptionItems.Add(item);

        db.Users.AddRange(doctorUser, otherDoctorUser, patientUser);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(caseEntity);
        db.Medicines.Add(med);
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.SendRemindersAsync(doctorId, profile.PatientProfileId,
                new RemindRequest(prescription.PrescriptionId), _nowUtc, TestContext.Current.CancellationToken));
    }

    #endregion
}
