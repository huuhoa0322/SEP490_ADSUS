using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// SCR-19 — cài đặt giờ nhắc uống thuốc. Chạy trên repository và PatientProfileService thật (DB
/// InMemory): hồ sơ bệnh nhân lấy qua module MedicalRecord, không đọc thẳng DbContext.
/// </summary>
public class ReminderPreferenceServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ReminderPreferenceService _sut;

    public ReminderPreferenceServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var patientProfiles = new PatientProfileService(
            new PatientProfileRepository(_db),
            new UserRepository(_db),
            NullLogger<PatientProfileService>.Instance);

        _sut = new ReminderPreferenceService(patientProfiles, new ReminderPreferenceRepository(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private Guid SeedPatient()
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            UserId = userId,
            FullName = "Bệnh nhân",
            Phone = "0900000001",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
        });
        _db.PatientProfiles.Add(new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = userId,
            CreatedBy = userId,
        });
        _db.SaveChanges();
        return userId;
    }

    [Fact]
    public async Task GetAsync_AccountWithoutProfile_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_NoPreferenceYet_ReturnsDefaultsWithoutCreatingRow()
    {
        var userId = SeedPatient();

        var result = await _sut.GetAsync(userId, TestContext.Current.CancellationToken);

        Assert.True(result.NotifEnabled);
        Assert.Equal("07:00", result.MorningTime);
        Assert.Equal("12:00", result.MiddayTime);
        Assert.Equal("20:00", result.EveningTime);
        Assert.Empty(await _db.PatientReminderPreferences.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertAsync_FirstTime_CreatesAndPersistsRow()
    {
        var userId = SeedPatient();

        await _sut.UpsertAsync(userId, new UpdateReminderPreferenceRequest(NotifEnabled: false, MorningTime: "06:30", MiddayTime: null, EveningTime: null), TestContext.Current.CancellationToken);

        var saved = await _sut.GetAsync(userId, TestContext.Current.CancellationToken);
        Assert.False(saved.NotifEnabled);
        Assert.Equal("06:30", saved.MorningTime);
        Assert.Equal("12:00", saved.MiddayTime); // trường không gửi lấy mặc định
        Assert.Single(await _db.PatientReminderPreferences.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertAsync_Existing_UpdatesOnlySentFields()
    {
        var userId = SeedPatient();
        await _sut.UpsertAsync(userId, new UpdateReminderPreferenceRequest(NotifEnabled: null, MorningTime: "06:30", MiddayTime: null, EveningTime: "21:00"),
            TestContext.Current.CancellationToken);

        await _sut.UpsertAsync(userId, new UpdateReminderPreferenceRequest(NotifEnabled: null, MorningTime: null, MiddayTime: "11:45", EveningTime: null),
            TestContext.Current.CancellationToken);

        var saved = await _sut.GetAsync(userId, TestContext.Current.CancellationToken);
        Assert.Equal("06:30", saved.MorningTime);
        Assert.Equal("11:45", saved.MiddayTime);
        Assert.Equal("21:00", saved.EveningTime);
        Assert.Single(await _db.PatientReminderPreferences.ToListAsync(TestContext.Current.CancellationToken));
    }
}
