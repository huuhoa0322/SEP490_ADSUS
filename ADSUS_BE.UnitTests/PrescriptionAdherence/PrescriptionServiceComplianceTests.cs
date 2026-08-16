using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.PrescriptionAdherence;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho Task 3 — GetCasePrescriptionsWithComplianceAsync.
/// Dùng real PrescriptionRepository + in-memory DB (đúng pattern MedicationIntakeLogRepositoryTests).
/// Chỉ mock IMedicationIntakeLogRepository.
/// Navigation chain bắt buộc: Doctor (User) vì Include trong ListByCaseAsync.
/// </summary>
public class PrescriptionServiceComplianceTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    /// <summary>
    /// Tạo chain: Medicine → Prescription → PrescriptionItem, kèm User stub cho Doctor navigation.
    /// Điều kiện: Doctor (User) bắt buộc vì EF query Include navigation.
    /// </summary>
    private static (Prescription p, PrescriptionItem item) SeedPrescription(
        AppDbContext db, Guid doctorId, Case caseEntity)
    {
        var med = new Medicine { MedicineId = Guid.NewGuid(), Name = "Thuốc A", CreatedAt = DateTime.UtcNow };
        var docUser = new User
        {
            UserId = doctorId,
            Email = "dr@test.com",
            PasswordHash = "stub",
            FullName = "Dr Test",
            Phone = "000",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var p = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = caseEntity.CaseId,
            DoctorId = doctorId,
            Doctor = docUser,
            PrescribedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = PrescriptionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Case = caseEntity,
            PrescriptionItems = new List<PrescriptionItem>(),
        };
        var item = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = p.PrescriptionId,
            Prescription = p,
            MedicineId = med.MedicineId,
            Medicine = med,
            Dosage = "1 viên",
            DurationDays = 3,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ScheduleSlots = new[] { ReminderSlot.Morning },
        };
        p.PrescriptionItems.Add(item);
        db.Medicines.Add(med);
        db.Users.Add(docUser);
        db.Cases.Add(caseEntity);
        db.Prescriptions.Add(p);
        return (p, item);
    }

    private static PrescriptionService CreateService(
        AppDbContext db,
        IMedicationIntakeLogRepository intakeRepo)
    {
        return new PrescriptionService(
            db,
            new PrescriptionRepository(db),
            new PrescriptionItemRepository(db),
            intakeRepo,
            Mock.Of<ICaseRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IMedicineRepository>(),
            Mock.Of<IMedicationIntakeScheduleGenerator>());
    }

    [Fact]
    public async Task GetCasePrescriptionsWithComplianceAsync_NoPrescription_ReturnsEmptyList()
    {
        using var db = CreateContext();
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var caseEntity = new Case
        {
            CaseId = caseId,
            PatientProfileId = Guid.NewGuid(),
            DoctorId = actorId,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.Cases.Add(caseEntity);
        await db.SaveChangesAsync();

        var intakeRepo = new Mock<IMedicationIntakeLogRepository>();
        var service = CreateService(db, intakeRepo.Object);

        var result = await service.GetCasePrescriptionsWithComplianceAsync(actorId, caseId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCasePrescriptionsWithComplianceAsync_OwnPrescription_ReturnsAdherence()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var caseEntity = new Case
        {
            CaseId = caseId,
            PatientProfileId = Guid.NewGuid(),
            DoctorId = doctorId,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.Cases.Add(caseEntity);
        var (p, item) = SeedPrescription(db, doctorId, caseEntity);
        await db.SaveChangesAsync();

        // 2 TAKEN + 1 PENDING = 3 total → 66.7%
        var intakeRepo = new Mock<IMedicationIntakeLogRepository>();
        intakeRepo.Setup(r => r.GetIntakeStatsByPrescriptionAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, IntakeStats>)new Dictionary<Guid, IntakeStats>
            {
                [item.PrescriptionItemId] = new IntakeStats(item.PrescriptionItemId, 3, 2, 1, 66.7),
            });

        var service = CreateService(db, intakeRepo.Object);

        var result = await service.GetCasePrescriptionsWithComplianceAsync(doctorId, caseId);

        Assert.Single(result);
        Assert.NotNull(result[0].AdherencePercent);
        Assert.Equal(66.7, result[0].AdherencePercent!.Value, 1);
        Assert.Single(result[0].Items);
        Assert.NotNull(result[0].Items[0].AdherencePercent);
        Assert.Equal(66.7, result[0].Items[0].AdherencePercent!.Value, 1);
    }

    [Fact]
    public async Task GetCasePrescriptionsWithComplianceAsync_OtherDoctorPrescription_ReturnsNullAdherence()
    {
        using var db = CreateContext();
        var ownDoctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var caseEntity = new Case
        {
            CaseId = caseId,
            PatientProfileId = Guid.NewGuid(),
            DoctorId = ownDoctorId,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.Cases.Add(caseEntity);
        var (p, _) = SeedPrescription(db, otherDoctorId, caseEntity);
        await db.SaveChangesAsync();

        var intakeRepo = new Mock<IMedicationIntakeLogRepository>();
        var service = CreateService(db, intakeRepo.Object);

        var result = await service.GetCasePrescriptionsWithComplianceAsync(ownDoctorId, caseId);

        Assert.Single(result);
        Assert.Null(result[0].AdherencePercent);
        Assert.All(result[0].Items, i => Assert.Null(i.AdherencePercent));
        intakeRepo.Verify(r => r.GetIntakeStatsByPrescriptionAsync(
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCasePrescriptionsWithComplianceAsync_NoLogsForOwnPrescription_ReturnsZeroAdherence()
    {
        using var db = CreateContext();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var caseEntity = new Case
        {
            CaseId = caseId,
            PatientProfileId = Guid.NewGuid(),
            DoctorId = doctorId,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.Cases.Add(caseEntity);
        var (p, item) = SeedPrescription(db, doctorId, caseEntity);
        await db.SaveChangesAsync();

        var intakeRepo = new Mock<IMedicationIntakeLogRepository>();
        intakeRepo.Setup(r => r.GetIntakeStatsByPrescriptionAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, IntakeStats>)new Dictionary<Guid, IntakeStats>
            {
                [item.PrescriptionItemId] = new IntakeStats(item.PrescriptionItemId, 0, 0, 0, 0),
            });

        var service = CreateService(db, intakeRepo.Object);

        var result = await service.GetCasePrescriptionsWithComplianceAsync(doctorId, caseId);

        Assert.Single(result);
        Assert.NotNull(result[0].AdherencePercent);
        Assert.Equal(0.0, result[0].AdherencePercent!.Value);
    }
}
