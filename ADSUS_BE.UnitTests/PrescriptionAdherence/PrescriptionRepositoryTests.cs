using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho PrescriptionRepository dùng Microsoft.EntityFrameworkCore.InMemory
/// (master convention — không cần Postgres thật). 5 case:
/// - GetByIdAsync returns entity with items + medicine + doctor navigation
/// - GetByIdAsync returns null khi không tồn tại
/// - ListByDoctorAsync sắp xếp theo PrescribedDate desc
/// - ListByDoctorAsync lọc đúng theo doctorId
/// - AddAsync chỉ add vào change tracker
///
/// Lưu ý: navigation non-nullable (Prescription.Doctor, MedicationIntakeLog.PrescriptionItem)
/// yêu cầu parent entity tồn tại trong DbContext để Include() resolve ở InMemory provider
/// (khác với Postgres thật — không enforce FK).
/// </summary>
public class PrescriptionRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static Medicine NewMedicine(string name = "Paracetamol")
        => new() { MedicineId = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };

    /// <summary>
    /// Stub User cho Doctor navigation. Role + Status là Postgres enums (AppDbContext
    /// HasPostgresEnum), KHÔNG có property C# — repo chỉ cần FK tồn tại để Include resolve.
    /// </summary>
    private static User NewDoctor(string phone = "0900000000")
        => new()
        {
            UserId = Guid.NewGuid(),
            Phone = phone,
            FullName = "BS. Test",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static Prescription NewPrescription(Guid doctorId, Guid caseId, DateOnly prescribedDate, DateTime createdAt)
        => new()
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = caseId,
            DoctorId = doctorId,
            PrescribedDate = prescribedDate,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task GetByIdAsync_ReturnsEntityWithItems()
    {
        using var db = CreateContext();
        var medicine = NewMedicine();
        await db.Medicines.AddAsync(medicine);

        // Repo Include(p => p.Doctor) cần User tồn tại trong DbContext để navigation resolve.
        // Master Prescription.Doctor = User với FK DoctorId → InMemory yêu cầu entity stub.
        var doctor = NewDoctor();
        await db.Users.AddAsync(doctor);

        // UC-11 detail navigation: Include Case → PatientProfile cần stub để InMemory
        // provider resolve (test trước đó chỉ truyền CaseId nhưng giờ cần entity).
        var patientUser = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0911111111",
            FullName = "Bệnh nhân Test",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await db.Users.AddAsync(patientUser);

        var patientProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUser.UserId,
            CreatedBy = doctor.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await db.PatientProfiles.AddAsync(patientProfile);

        var caseEntity = new Case
        {
            CaseId = Guid.NewGuid(),
            PatientProfileId = patientProfile.PatientProfileId,
            DoctorId = doctor.UserId,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await db.Cases.AddAsync(caseEntity);

        var prescription = NewPrescription(doctor.UserId, caseEntity.CaseId, DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow);
        var item = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = prescription.PrescriptionId,
            MedicineId = medicine.MedicineId,
            Dosage = "1 viên",
            DurationDays = 5,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        prescription.PrescriptionItems.Add(item);
        await db.Prescriptions.AddAsync(prescription);
        await db.SaveChangesAsync();

        var repo = new PrescriptionRepository(db);
        var fetched = await repo.GetByIdAsync(prescription.PrescriptionId);

        Assert.NotNull(fetched);
        Assert.Single(fetched!.PrescriptionItems);
        Assert.Equal("1 viên", fetched.PrescriptionItems.First().Dosage);
        Assert.Equal("Paracetamol", fetched.PrescriptionItems.First().Medicine!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        using var db = CreateContext();
        var repo = new PrescriptionRepository(db);

        var fetched = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(fetched);
    }

    [Fact]
    public async Task ListByDoctorAsync_OrdersByPrescribedDateDescending()
    {
        using var db = CreateContext();
        var doctor = Guid.NewGuid();

        var oldPrescription = NewPrescription(
            doctor, Guid.NewGuid(),
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));
        var newPrescription = NewPrescription(
            doctor, Guid.NewGuid(),
            new DateOnly(2026, 7, 28),
            new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc));
        await db.Prescriptions.AddRangeAsync(oldPrescription, newPrescription);
        await db.SaveChangesAsync();

        var repo = new PrescriptionRepository(db);
        var list = await repo.ListByDoctorAsync(doctor);

        Assert.Equal(2, list.Count);
        Assert.Equal(newPrescription.PrescriptionId, list[0].PrescriptionId);
    }

    [Fact]
    public async Task ListByDoctorAsync_FiltersByDoctor()
    {
        using var db = CreateContext();
        var doctor1 = Guid.NewGuid();
        var doctor2 = Guid.NewGuid();
        await db.Prescriptions.AddRangeAsync(
            NewPrescription(doctor1, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow),
            NewPrescription(doctor2, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow));
        await db.SaveChangesAsync();

        var repo = new PrescriptionRepository(db);
        var d1List = await repo.ListByDoctorAsync(doctor1);

        Assert.Single(d1List);
        Assert.Equal(doctor1, d1List[0].DoctorId);
    }

    [Fact]
    public async Task AddAsync_AddsToChangeTracker()
    {
        using var db = CreateContext();
        var repo = new PrescriptionRepository(db);

        var p = NewPrescription(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow);
        await repo.AddAsync(p);
        await db.SaveChangesAsync();

        var fetched = await db.Prescriptions.FindAsync(p.PrescriptionId);
        Assert.NotNull(fetched);
    }
}