using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Adversarial Challenge Test Harness for Backend Relative Booking:
/// - Rigorous stress-testing of Pool 1, 2, 3 limits and bypass vectors.
/// - Same-day active booking boundaries (same vs distinct patients, cancelled slot reuse).
/// - Elderly anti-merge edge cases (null, empty, whitespace phone inputs).
/// - EF Core Npgsql SQL compilation & Shadow Property elimination (PostgreSQL 42703 prevention).
/// </summary>
public class AdversarialBackendChallengeTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICaseService> _caseService = new();
    private readonly Mock<IPatientRelationshipRepository> _relationshipRepo = new();
    private readonly AppointmentService _appointmentService;
    private readonly PatientRelationshipService _relationshipService;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _userProfileId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();

    public AdversarialBackendChallengeTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        var doctor = new User
        {
            UserId = _doctorId,
            FullName = "BS. Adversarial Tester",
            Phone = "0987654321",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(doctor);

        var currentUser = new User
        {
            UserId = _userId,
            FullName = "Nguyễn Văn Adversary",
            Phone = "0912345678",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var userProfile = new PatientProfile
        {
            PatientProfileId = _userProfileId,
            UserId = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(currentUser);
        _db.PatientProfiles.Add(userProfile);
        _db.SaveChanges();

        _profileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.PatientProfiles.Find(id));

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Callback<Appointment, CancellationToken>((appt, _) => _db.Appointments.Add(appt))
            .ReturnsAsync((Appointment appt, CancellationToken _) => appt);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _caseService.Setup(c => c.CreateFromBookingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<SymptomInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var noShowSettings = Options.Create(new NoShowSettings { GraceTimeMinutes = 15 });
        var noShowService = new NoShowService(
            _db,
            noShowSettings,
            _notificationService.Object,
            _profileRepo.Object,
            Mock.Of<ILogger<NoShowService>>());

        _appointmentService = new AppointmentService(
            _appointmentRepo.BackedBy(_db).Object,
            _slotRepo.BackedBy(_db).Object,
            new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db),
            new ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService(_profileRepo.Object, new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db), Microsoft.Extensions.Logging.Abstractions.NullLogger<ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService>.Instance),
            new ADSUS_BE.BLL.PatientRelationship.Services.PatientRelationshipService(new ADSUS_BE.DAL.Repositories.Implementations.PatientRelationshipRepository(_db), _profileRepo.Object, _db),
            _notificationService.Object,
            _caseService.BackedBy(_db).Object,
            noShowService,
            _db,
            Mock.Of<ILogger<AppointmentService>>());

        _relationshipService = new PatientRelationshipService(
            _relationshipRepo.Object,
            _profileRepo.Object,
            _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private ScheduleSlot CreateSlot(DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctorId,
            SlotDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Status = SlotStatus.Open,
            CreatedAt = DateTime.UtcNow,
            Doctor = _db.Users.Find(_doctorId)!,
            Appointments = new List<Appointment>()
        };
        _db.ScheduleSlots.Add(slot);
        _db.SaveChanges();

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(slot.SlotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        return slot;
    }

    #region ADV-SHADOW: EF Core Model & Npgsql SQL Translation Verification

    [Fact]
    public void ADV_SHADOW_01_NpgsqlModel_ZeroShadowPropertiesOnTargetEntities()
    {
        // Build an AppDbContext model using Npgsql provider (without connecting)
        var npgsqlOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .Options;

        using var npgsqlDb = new AppDbContext(npgsqlOptions);
        var model = npgsqlDb.Model;

        var targetEntities = new[] { typeof(Appointment), typeof(PatientRelationship), typeof(User), typeof(PatientProfile) };

        foreach (var entityType in targetEntities)
        {
            var efEntity = model.FindEntityType(entityType);
            Assert.NotNull(efEntity);

            var shadowProps = efEntity.GetProperties()
                .Where(p => p.IsShadowProperty())
                .Select(p => p.Name)
                .ToList();

            // Specifically verify zero shadow properties on Appointment and PatientRelationship
            if (entityType == typeof(Appointment) || entityType == typeof(PatientRelationship))
            {
                Assert.True(shadowProps.Count == 0,
                    $"Entity {entityType.Name} contains unexpected shadow properties: {string.Join(", ", shadowProps)}");
            }
        }
    }

    [Fact]
    public void ADV_SHADOW_02_NoShowJobQuery_TranslatesToNpgsqlSqlWithoutShadowColumns()
    {
        var npgsqlOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .Options;

        using var npgsqlDb = new AppDbContext(npgsqlOptions);

        // This query matches NoShowCancellationJob.Execute
        var query = npgsqlDb.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.Status == AppointmentStatus.Booked)
            .Where(a => a.Slot != null);

        var sql = query.ToQueryString();
        Assert.NotNull(sql);

        // PostgreSQL error 42703 causes: "a.user_id", "a.patient_relationship_relationship_id", "patient_profile_id1"
        Assert.DoesNotContain("user_id1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient_profile_id1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient_relationship_relationship_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADV_SHADOW_03_AppointmentFullIncludeQuery_TranslatesToNpgsqlSqlWithoutShadowColumns()
    {
        var npgsqlOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy;Username=dummy;Password=dummy")
            .Options;

        using var npgsqlDb = new AppDbContext(npgsqlOptions);

        var query = npgsqlDb.Appointments
            .Include(a => a.Slot).ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
            .Include(a => a.BookedByUser)
            .Include(a => a.PatientRelationship);

        var sql = query.ToQueryString();
        Assert.NotNull(sql);

        // Verify mapped foreign keys: booked_by_user_id and relationship_id are present
        Assert.Contains("booked_by_user_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relationship_id", sql, StringComparison.OrdinalIgnoreCase);

        // Verify non-existent shadow columns are absent
        Assert.DoesNotContain("user_id1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient_profile_id1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient_relationship_relationship_id", sql, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ADV-POOL: 3 Limit Pools Stress-Testing & Bypass Resistance

    [Fact]
    public async Task ADV_POOL_01_Pool1_ActiveBookingsCount_BookedCountsTowardsSelfLimit()
    {
        // 3 Booked for self = 3 active
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        for (int i = 0; i < 3; i++)
        {
            var date = futureDate.AddDays(i);
            var slot = CreateSlot(date, new TimeOnly(8, 0), new TimeOnly(9, 0));
            _db.Appointments.Add(new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = null,
                RelationshipId = null,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        _db.SaveChanges();

        var targetSlot = CreateSlot(futureDate.AddDays(4), new TimeOnly(14, 0), new TimeOnly(15, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            RelationshipId = null
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("3 lịch hẹn đang chờ", ex.Message);
    }

    [Fact]
    public async Task ADV_POOL_02_Pool2_BookedForOthers_IndependentFromSelfBookings()
    {
        // User has 3 self bookings (Pool 1 maxed)
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        for (int i = 0; i < 3; i++)
        {
            var slot = CreateSlot(futureDate.AddDays(i), new TimeOnly(8, 0), new TimeOnly(9, 0));
            _db.Appointments.Add(new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = null,
                RelationshipId = null,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        _db.SaveChanges();

        // User books for Relative 1 (Pool 2 has 0 active bookings) -> Must succeed!
        var relProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            FullName = "Người thân Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var rel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = relProfile.PatientProfileId,
            RelationshipName = "Em",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(relProfile);
        _db.PatientRelationships.Add(rel);
        _db.SaveChanges();

        var relSlot = CreateSlot(futureDate.AddDays(4), new TimeOnly(10, 0), new TimeOnly(11, 0));
        var relRequest = new BookAppointmentRequest
        {
            ScheduleSlotId = relSlot.SlotId,
            RelationshipId = rel.RelationshipId
        };

        var response = await _appointmentService.BookAppointmentAsync(_userId, _userProfileId, relRequest, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(AppointmentStatus.Booked, response.Status);
        Assert.True(response.IsBookedForOthers);
    }

    [Fact]
    public async Task ADV_POOL_03_Pool3_SystemWidePatientLimit_StrictlyEnforcedEvenWithDifferentBookers()
    {
        // Patient P has 3 active appointments:
        // 1 self-booked, 1 booked by Parent, 1 booked by Sibling
        var patientProfileId = Guid.NewGuid();
        var patientProfile = new PatientProfile
        {
            PatientProfileId = patientProfileId,
            FullName = "Bệnh Nhân Chung",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(patientProfile);

        var parentUserId = Guid.NewGuid();
        var siblingUserId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));

        // 1 self
        var s1 = CreateSlot(futureDate, new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = s1.SlotId,
            PatientProfileId = patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 1 by parent
        var s2 = CreateSlot(futureDate.AddDays(1), new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = s2.SlotId,
            PatientProfileId = patientProfileId,
            BookedByUserId = parentUserId,
            RelationshipId = Guid.NewGuid(),
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 1 by sibling
        var s3 = CreateSlot(futureDate.AddDays(2), new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = s3.SlotId,
            PatientProfileId = patientProfileId,
            BookedByUserId = siblingUserId,
            RelationshipId = Guid.NewGuid(),
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        // Now, current user tries to book for this same patient as their relative
        var adversaryRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = patientProfileId,
            RelationshipName = "Bạn",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(adversaryRel);
        _db.SaveChanges();

        var s4 = CreateSlot(futureDate.AddDays(3), new TimeOnly(8, 0), new TimeOnly(9, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = s4.SlotId,
            RelationshipId = adversaryRel.RelationshipId
        };

        // Act & Assert: Pool 3 must block this!
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Bệnh nhân này đã có tối đa 3 lịch hẹn", ex.Message);
    }

    [Fact]
    public async Task ADV_POOL_04_QuotaRelease_CancelledAndNoShowFreeUpPoolSlots()
    {
        // User has 3 appointments: 1 Cancelled, 1 NoShow, 1 Booked -> active count = 1
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6));

        var s1 = CreateSlot(futureDate, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var s2 = CreateSlot(futureDate.AddDays(1), new TimeOnly(8, 0), new TimeOnly(9, 0));
        var s3 = CreateSlot(futureDate.AddDays(2), new TimeOnly(8, 0), new TimeOnly(9, 0));

        _db.Appointments.AddRange(
            new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = s1.SlotId,
                PatientProfileId = _userProfileId,
                Status = AppointmentStatus.Cancelled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = s2.SlotId,
                PatientProfileId = _userProfileId,
                Status = AppointmentStatus.NoShow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = s3.SlotId,
                PatientProfileId = _userProfileId,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        _db.SaveChanges();

        // 2nd active booking should succeed
        var s4 = CreateSlot(futureDate.AddDays(3), new TimeOnly(8, 0), new TimeOnly(9, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = s4.SlotId,
            RelationshipId = null
        };

        var response = await _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(AppointmentStatus.Booked, response.Status);
    }

    #endregion

    #region ADV-SAMEDAY: 1 Active Appointment Per Day Rule Boundaries

    [Fact]
    public async Task ADV_SAMEDAY_01_CancelledAppointmentOnSameDay_AllowsRebookingOnSameDay()
    {
        // Patient has a CANCELLED appointment on date D
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var s1 = CreateSlot(targetDate, new TimeOnly(8, 0), new TimeOnly(9, 0));

        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = s1.SlotId,
            Slot = s1,
            PatientProfileId = _userProfileId,
            Status = AppointmentStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        // New slot on the SAME date D at 14:00
        var s2 = CreateSlot(targetDate, new TimeOnly(14, 0), new TimeOnly(15, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = s2.SlotId,
            RelationshipId = null
        };

        // Must succeed because the earlier appointment was CANCELLED
        var response = await _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal(AppointmentStatus.Booked, response.Status);
    }

    [Fact]
    public async Task ADV_SAMEDAY_02_CrossUserSameDayBooking_BlockedForSamePatient()
    {
        // Patient P has a booked slot on date D booked by User 1
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9));
        var patientProfileId = Guid.NewGuid();
        var patientProfile = new PatientProfile
        {
            PatientProfileId = patientProfileId,
            FullName = "Bệnh Nhân Một Ngày",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(patientProfile);

        var s1 = CreateSlot(targetDate, new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = s1.SlotId,
            Slot = s1,
            PatientProfileId = patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // User 2 adds patient P to their relationships
        var user2Rel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = patientProfileId,
            RelationshipName = "Người thân",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(user2Rel);
        _db.SaveChanges();

        // User 2 tries to book for patient P on the same date D at 15:00
        var s2 = CreateSlot(targetDate, new TimeOnly(15, 0), new TimeOnly(16, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = s2.SlotId,
            RelationshipId = user2Rel.RelationshipId
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Mỗi ngày chỉ được đặt tối đa 1 lịch", ex.Message);
    }

    #endregion

    #region ADV-ELDERLY: Elderly Anti-Merge Edge Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public async Task ADV_ELDERLY_01_EmptyOrWhitespacePhone_BypassesLookupAndCreatesDistinctProfile(string? inputPhone)
    {
        var request1 = new AddRelativeRequest(
            FullName: "Cụ Bà",
            Phone: inputPhone,
            DateOfBirth: new DateOnly(1930, 1, 1),
            RelationshipName: "Bà Cố");

        var request2 = new AddRelativeRequest(
            FullName: "Cụ Ông",
            Phone: inputPhone,
            DateOfBirth: new DateOnly(1928, 5, 5),
            RelationshipName: "Ông Cố");

        var res1 = await _relationshipService.AddRelativeAsync(request1, _userId, TestContext.Current.CancellationToken);
        var res2 = await _relationshipService.AddRelativeAsync(request2, _userId, TestContext.Current.CancellationToken);

        Assert.NotNull(res1);
        Assert.NotNull(res2);
        Assert.NotEqual(res1.PatientProfileId, res2.PatientProfileId);

        var p1 = await _db.PatientProfiles.FindAsync(new object[] { res1.PatientProfileId }, TestContext.Current.CancellationToken);
        var p2 = await _db.PatientProfiles.FindAsync(new object[] { res2.PatientProfileId }, TestContext.Current.CancellationToken);

        Assert.NotNull(p1);
        Assert.NotNull(p2);
        Assert.Null(p1.Phone);
        Assert.Null(p2.Phone);
        Assert.Equal(_userId, p1.CreatedBy);
        Assert.Equal(_userId, p2.CreatedBy);
        Assert.Equal("Cụ Bà", p1.FullName);
        Assert.Equal("Cụ Ông", p2.FullName);
    }

    [Fact]
    public async Task ADV_ELDERLY_02_TwoDifferentUsers_AddElderly_NoCrossUserCollision()
    {
        var user2Id = Guid.NewGuid();
        var user2 = new User
        {
            UserId = user2Id,
            FullName = "User 2",
            Phone = "0944555666",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user2);
        _db.SaveChanges();

        var req1 = new AddRelativeRequest("Bà A", null, null, "Bà");
        var req2 = new AddRelativeRequest("Bà A", null, null, "Bà");

        var res1 = await _relationshipService.AddRelativeAsync(req1, _userId, TestContext.Current.CancellationToken);
        var res2 = await _relationshipService.AddRelativeAsync(req2, user2Id, TestContext.Current.CancellationToken);

        Assert.NotEqual(res1.PatientProfileId, res2.PatientProfileId);

        var p1 = await _db.PatientProfiles.FindAsync(new object[] { res1.PatientProfileId }, TestContext.Current.CancellationToken);
        var p2 = await _db.PatientProfiles.FindAsync(new object[] { res2.PatientProfileId }, TestContext.Current.CancellationToken);

        Assert.NotNull(p1);
        Assert.NotNull(p2);
        Assert.Equal(_userId, p1.CreatedBy);
        Assert.Equal(user2Id, p2.CreatedBy);
    }

    #endregion
}
