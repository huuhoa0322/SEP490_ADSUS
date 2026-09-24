using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// UC-11 / UC-17 — lịch uống thuốc của bệnh nhân. Chạy trên repository và PatientProfileService
/// thật (DB InMemory): hồ sơ bệnh nhân lấy qua module MedicalRecord, không đọc thẳng DbContext.
/// </summary>
public class MedicationIntakeServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly MedicationIntakeService _sut;
    private readonly User _doctor;

    public MedicationIntakeServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var patientProfiles = new PatientProfileService(
            new PatientProfileRepository(_db),
            new UserRepository(_db),
            NullLogger<PatientProfileService>.Instance);

        _sut = new MedicationIntakeService(
            patientProfiles,
            new MedicationIntakeLogRepository(_db),
            new PrescriptionRepository(_db),
            Mock.Of<INotificationService>());

        _doctor = NewUser(UserRole.Doctor);
        _db.Users.Add(_doctor);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static User NewUser(UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        FullName = role.ToString(),
        Phone = "09" + Random.Shared.Next(10000000, 99999999),
        PasswordHash = "hash",
        Role = role,
        Status = UserStatus.Active,
    };

    /// <summary>Tài khoản bệnh nhân + hồ sơ. Trả (UserId, PatientProfileId).</summary>
    private (Guid UserId, Guid ProfileId) SeedPatient()
    {
        var user = NewUser(UserRole.Patient);
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = user.UserId, CreatedBy = user.UserId };
        _db.Users.Add(user);
        _db.PatientProfiles.Add(profile);
        _db.SaveChanges();
        return (user.UserId, profile.PatientProfileId);
    }

    /// <summary>Đơn thuốc 2 dòng; mỗi phần tử <paramref name="confirmedPerDose"/> là 1 liều, true = đã uống.</summary>
    private Guid SeedPrescription(Guid profileId, params bool[] confirmedPerDose)
    {
        var medicine = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol" };
        var medicalCase = new Case { CaseId = Guid.NewGuid(), PatientProfileId = profileId, DoctorId = _doctor.UserId };
        var prescription = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            DoctorId = _doctor.UserId,
            PrescribedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        var items = Enumerable.Range(0, 2).Select(_ => new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = prescription.PrescriptionId,
            MedicineId = medicine.MedicineId,
            Dosage = "1 viên",
            DurationDays = 1,
            StartDate = prescription.PrescribedDate,
        }).ToList();

        _db.Medicines.Add(medicine);
        _db.Cases.Add(medicalCase);
        _db.Prescriptions.Add(prescription);
        _db.PrescriptionItems.AddRange(items);
        for (var i = 0; i < confirmedPerDose.Length; i++)
        {
            var scheduled = DateTime.UtcNow.AddHours(-3 - i);
            _db.MedicationIntakeLogs.Add(new MedicationIntakeLog
            {
                IntakeId = Guid.NewGuid(),
                PrescriptionItemId = items[i % items.Count].PrescriptionItemId, // rải liều qua cả 2 dòng thuốc
                ScheduledTime = scheduled,
                ConfirmedAt = confirmedPerDose[i] ? scheduled.AddMinutes(5) : null,
                Status = confirmedPerDose[i] ? IntakeStatus.Taken : IntakeStatus.Pending,
            });
        }
        _db.SaveChanges();
        return prescription.PrescriptionId;
    }

    [Fact]
    public async Task ListUpcomingAsync_AccountWithoutProfile_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.ListUpcomingAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListByPrescriptionAsync_OwnPrescription_ReturnsEveryDose()
    {
        var (userId, profileId) = SeedPatient();
        var prescriptionId = SeedPrescription(profileId, true, false, false);

        var result = await _sut.ListByPrescriptionAsync(userId, prescriptionId, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ListByPrescriptionAsync_AnotherPatientsPrescription_ThrowsUnauthorized()
    {
        var (_, ownerProfileId) = SeedPatient();
        var (otherUserId, _) = SeedPatient();
        var prescriptionId = SeedPrescription(ownerProfileId, false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ListByPrescriptionAsync(otherUserId, prescriptionId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAdherenceAsync_CountsDosesAcrossAllItemsOfPrescription()
    {
        var (_, profileId) = SeedPatient();
        var prescriptionId = SeedPrescription(profileId, true, false, true, false);
        SeedPrescription(profileId, true, true); // đơn khác — không được tính vào

        var result = await _sut.GetAdherenceAsync(prescriptionId, TestContext.Current.CancellationToken);

        Assert.Equal(4, result.TotalDoses);
        Assert.Equal(2, result.TakenDoses);
        Assert.Equal(2, result.PendingDoses);
    }
}
