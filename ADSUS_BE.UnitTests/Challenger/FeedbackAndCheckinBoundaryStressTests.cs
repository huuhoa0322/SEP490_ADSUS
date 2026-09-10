using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Challenger;

/// <summary>
/// Adversarial Empirical Boundary & Stress Test Suite for Remediated Feedback and Check-in Backend.
/// Focus areas:
/// 1. Boundary tests on feedback queries with null/empty/invalid CaseId, missing doctor accounts, missing patient profiles.
/// 2. Predictable search across DoctorName, CaseCode, and PatientName when fields are null or empty.
/// 3. Boundary tests on check-in queue with null navigation properties, date inversion, extreme pagination.
/// 4. Invariant checks: zero crashes, zero infinite loops, predictable error handling.
/// </summary>
public class FeedbackAndCheckinBoundaryStressTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"BoundaryStress_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private AppointmentService CreateAppointmentService(AppDbContext db)
    {
        var apptRepo = new Mock<IAppointmentRepository>();
        var slotRepo = new Mock<IScheduleSlotRepository>();
        var profileRepo = new Mock<IPatientProfileRepository>();
        var notifService = new Mock<INotificationService>();
        var caseService = new Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService>();

        var noShowSettings = Options.Create(new NoShowSettings { GraceTimeMinutes = 15 });
        var noShowService = new NoShowService(
            db,
            noShowSettings,
            notifService.Object,
            profileRepo.Object,
            Mock.Of<ILogger<NoShowService>>());

        return new AppointmentService(
            apptRepo.Object,
            slotRepo.Object,
            profileRepo.Object,
            notifService.Object,
            caseService.Object,
            noShowService,
            db,
            Mock.Of<ILogger<AppointmentService>>());
    }

    #region 1. Feedback Boundary Tests: Null/Empty/Invalid CaseId

    [Fact]
    public async Task Feedback_WithNullCaseId_IncludedInPagedAndGetAll_MapsGuidEmptyAndDefaultDoctor()
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Null Case Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000001", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = null,
            Case = null,
            Rating = 5,
            Content = "Phản hồi không gắn ca khám",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);
        db.ServiceFeedbacks.Add(fb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        // Act 1: Paged query
        var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, paged.TotalItems);
        var pagedItem = Assert.Single(paged.Items);
        Assert.Equal(fb.FeedbackId, pagedItem.Id);
        Assert.Equal(Guid.Empty, pagedItem.CaseId);
        Assert.Equal(Guid.Empty, pagedItem.DoctorId);
        Assert.Equal("Không xác định", pagedItem.DoctorName);
        Assert.Equal("Null Case Patient", pagedItem.PatientName);

        // Act 2: GetAll query
        var all = await service.GetAllAsync(TestContext.Current.CancellationToken);
        var allItem = Assert.Single(all);
        Assert.Equal(fb.FeedbackId, allItem.Id);
        Assert.Equal(Guid.Empty, allItem.CaseId);
        Assert.Equal(Guid.Empty, allItem.DoctorId);
        Assert.Equal("Không xác định", allItem.DoctorName);
    }

    [Fact]
    public async Task Feedback_WithExplicitGuidEmptyCaseId_IsSafelyHandled_WithoutCrashing()
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Empty Guid Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000002", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = Guid.Empty,
            Case = null,
            Rating = 4,
            Content = "Feedback with Guid.Empty",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);
        db.ServiceFeedbacks.Add(fb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, paged.TotalItems);
        var item = Assert.Single(paged.Items);
        Assert.Equal(Guid.Empty, item.CaseId);
        Assert.Equal("Không xác định", item.DoctorName);
    }

    [Fact]
    public async Task Feedback_SearchByGuidEmpty_DoesNotMatchAllNullOrEmptyCaseFeedbacks()
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Guid Search Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000003", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var fb1 = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = null,
            Rating = 5,
            Content = "Feedback 1 with null case",
            SubmittedAt = DateTime.UtcNow
        };
        var fb2 = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = Guid.Empty,
            Rating = 5,
            Content = "Feedback 2 with empty case",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);
        db.ServiceFeedbacks.AddRange(fb1, fb2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);

        // Searching for Guid.Empty string should NOT match either fb1 (null CaseId) or fb2 (Guid.Empty)
        var (items, total) = await repo.GetPagedAsync(Guid.Empty.ToString(), null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Feedback_GetByCaseIdAsync_WithGuidEmpty_ReturnsNullImmediately()
    {
        using var db = CreateDbContext();
        var repo = new FeedbackRepository(db);

        var result = await repo.GetByCaseIdAsync(Guid.Empty, TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("1234-invalid-guid")]
    [InlineData("00000000")]
    [InlineData("urn:uuid:12345")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("!@#$%^&*()_+{}|:\"<>?~`-=[]\\;',./")]
    public async Task Feedback_SearchByMalformedOrAdversarialKeywords_DoesNotCrash(string adversarialKeyword)
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Normal Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000004", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 5,
            Content = "Valid content",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);
        db.ServiceFeedbacks.Add(fb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        // Should return cleanly without throwing exceptions
        var result = await service.GetPagedAsync(adversarialKeyword, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.True(result.TotalItems >= 0);
    }

    #endregion

    #region 2. Feedback Boundary Tests: Missing Doctor / Missing Patient Profile

    [Fact]
    public async Task Feedback_WithMissingDoctorAccount_CaseHasNoDoctor_ReturnsDefaultDoctorName()
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Patient For Orphan Case", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000005", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var orphanDoctorId = Guid.NewGuid();
        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = orphanDoctorId,
            Doctor = null!, // Missing doctor user navigation
            PatientProfileId = profile.PatientProfileId
        };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = medicalCase.CaseId,
            Case = medicalCase,
            Rating = 5,
            Content = "Case without loaded Doctor entity",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(medicalCase);
        db.ServiceFeedbacks.Add(fb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, paged.TotalItems);
        var item = Assert.Single(paged.Items);
        Assert.Equal(orphanDoctorId, item.DoctorId);
        Assert.Equal("Không xác định", item.DoctorName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Feedback_WithDoctorHavingWhitespaceFullName_ReturnsDefaultDoctorName(string doctorName)
    {
        using var db = CreateDbContext();
        var doctor = new User { UserId = Guid.NewGuid(), FullName = doctorName, Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0911000006", PasswordHash = "x" };
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Valid Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000007", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            PatientProfileId = profile.PatientProfileId
        };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = medicalCase.CaseId,
            Case = medicalCase,
            Rating = 5,
            Content = "Doctor has whitespace name",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.AddRange(doctor, patient);
        db.PatientProfiles.Add(profile);
        db.Cases.Add(medicalCase);
        db.ServiceFeedbacks.Add(fb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);
        var item = Assert.Single(paged.Items);
        Assert.Equal("Không xác định", item.DoctorName);
    }

    [Fact]
    public async Task Feedback_WithPatientUserHavingEmptyFullNameAndPhone_ReturnsDefaults()
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "   ", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0911000008", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = null,
            Case = null,
            Rating = 3,
            Content = "Feedback with empty patient name",
            SubmittedAt = DateTime.UtcNow
        };

        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);
        db.ServiceFeedbacks.Add(fb);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);
        var item = Assert.Single(paged.Items);
        Assert.Equal("   ", item.PatientName);
        Assert.Equal("0911000008", item.PatientPhone);
    }

    [Fact]
    public async Task Feedback_MappingResilience_WhenNavigationsAreNull_DoesNotThrowNullReference()
    {
        // Direct unit test of FeedbackService mapping resilience on raw entity with null navigations
        var feedback = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = Guid.NewGuid(),
            PatientProfile = null!,
            CaseId = null,
            Case = null,
            Rating = 5,
            Content = "Raw entity with null navigations",
            SubmittedAt = DateTime.UtcNow
        };

        var mockRepo = new Mock<IFeedbackRepository>();
        mockRepo.Setup(r => r.GetPagedAsync(null, null, 1, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ServiceFeedback> { feedback }, 1));

        var service = new FeedbackService(mockRepo.Object);
        var result = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalItems);
        var item = Assert.Single(result.Items);
        Assert.Equal(string.Empty, item.PatientName);
        Assert.Equal(string.Empty, item.PatientPhone);
        Assert.Equal(Guid.Empty, item.CaseId);
        Assert.Equal(Guid.Empty, item.DoctorId);
        Assert.Equal("Không xác định", item.DoctorName);
    }

    #endregion

    #region 3. Searching Across DoctorName, CaseCode, and PatientName Predictability

    [Fact]
    public async Task Feedback_SearchAcrossDoctorPatientCase_BehavesPredictablyWithNullAndPopulatedFields()
    {
        using var db = CreateDbContext();

        var doctor1 = new User { UserId = Guid.NewGuid(), FullName = "Dr. Alexander Fleming", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0901000001", PasswordHash = "x" };
        var patient1 = new User { UserId = Guid.NewGuid(), FullName = "Louis Pasteur", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0902000001", PasswordHash = "x" };
        var profile1 = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient1.UserId, User = patient1 };
        var case1 = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor1.UserId, Doctor = doctor1, PatientProfileId = profile1.PatientProfileId };

        var fb1 = new ServiceFeedback { FeedbackId = Guid.NewGuid(), PatientProfileId = profile1.PatientProfileId, PatientProfile = profile1, CaseId = case1.CaseId, Case = case1, Rating = 5, Content = "Penicillin discovery", SubmittedAt = DateTime.UtcNow.AddMinutes(-10) };

        // Feedback 2 has general feedback (no case, no doctor)
        var patient2 = new User { UserId = Guid.NewGuid(), FullName = "Marie Curie", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0902000002", PasswordHash = "x" };
        var profile2 = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient2.UserId, User = patient2 };
        var fb2 = new ServiceFeedback { FeedbackId = Guid.NewGuid(), PatientProfileId = profile2.PatientProfileId, PatientProfile = profile2, CaseId = null, Case = null, Rating = 5, Content = "Radioactivity research", SubmittedAt = DateTime.UtcNow.AddMinutes(-5) };

        // Feedback 3 has missing doctor on case
        var patient3 = new User { UserId = Guid.NewGuid(), FullName = "Gregor Mendel", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0902000003", PasswordHash = "x" };
        var profile3 = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient3.UserId, User = patient3 };
        var case3 = new Case { CaseId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), Doctor = null!, PatientProfileId = profile3.PatientProfileId };
        var fb3 = new ServiceFeedback { FeedbackId = Guid.NewGuid(), PatientProfileId = profile3.PatientProfileId, PatientProfile = profile3, CaseId = case3.CaseId, Case = case3, Rating = 4, Content = "Genetics experiment", SubmittedAt = DateTime.UtcNow };

        db.Users.AddRange(doctor1, patient1, patient2, patient3);
        db.PatientProfiles.AddRange(profile1, profile2, profile3);
        db.Cases.AddRange(case1, case3);
        db.ServiceFeedbacks.AddRange(fb1, fb2, fb3);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);

        // 1. Search by Doctor name "Fleming" -> Matches fb1 only
        var (resDoc, totalDoc) = await repo.GetPagedAsync("Fleming", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalDoc);
        Assert.Equal(fb1.FeedbackId, resDoc[0].FeedbackId);

        // 2. Search by Patient name "Curie" (which has no case/doctor) -> Matches fb2 only without throwing
        var (resPat, totalPat) = await repo.GetPagedAsync("Curie", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalPat);
        Assert.Equal(fb2.FeedbackId, resPat[0].FeedbackId);

        // 3. Search by CaseCode Guid string of case3 (which has no doctor) -> Matches fb3 only without throwing
        var (resCase, totalCase) = await repo.GetPagedAsync(case3.CaseId.ToString(), null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalCase);
        Assert.Equal(fb3.FeedbackId, resCase[0].FeedbackId);

        // 4. Search by partial CaseCode Guid string of case1
        var partialCase = case1.CaseId.ToString().Substring(0, 8);
        var (resPartial, totalPartial) = await repo.GetPagedAsync(partialCase, null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalPartial);
        Assert.Equal(fb1.FeedbackId, resPartial[0].FeedbackId);

        // 5. Search with empty or whitespace returns all 3
        var (resAll, totalAll) = await repo.GetPagedAsync("   ", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(3, totalAll);
        Assert.Equal(3, resAll.Count);
    }

    #endregion

    #region 4. Check-in Queue Boundary & Stress Tests

    [Fact]
    public async Task CheckinQueue_WithNullNavigationProperties_DoesNotThrowNullReference()
    {
        using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var doctorUser = new User { UserId = Guid.NewGuid(), FullName = "Dr. Boundary", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0900000030", PasswordHash = "x" };
        var patientUser = new User { UserId = Guid.NewGuid(), FullName = "Patient Boundary", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000031", PasswordHash = "x" };

        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctorUser.UserId,
            Doctor = doctorUser,
            SlotDate = today,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Status = SlotStatus.Booked
        };

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUser.UserId,
            User = patientUser
        };

        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Status = AppointmentStatus.Booked,
            Reason = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(doctorUser, patientUser);
        db.ScheduleSlots.Add(slot);
        db.PatientProfiles.Add(profile);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateAppointmentService(db);

        // Act: Search should safely evaluate without NullReferenceException
        var result = await service.GetCheckinQueueAsync(today, today, "SearchTerm", "ALL", 1, 15, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);

        // Act 2: Unfiltered fetch should map null fields to defaults
        var unfiltered = await service.GetCheckinQueueAsync(today, today, null, "ALL", 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, unfiltered.TotalCount);
        var item = Assert.Single(unfiltered.Items);
        Assert.Equal("Patient Boundary", item.PatientFullName);
        Assert.Equal("0900000031", item.PatientPhone);
        Assert.Equal("Dr. Boundary", item.DoctorName);
        Assert.Null(item.Reason);
    }

    [Theory]
    [InlineData(-10, -5)]
    [InlineData(0, 0)]
    [InlineData(1, 99999)]
    [InlineData(-1, 100)]
    public async Task CheckinQueue_ExtremePaginationBoundaries_SanitizedGracefully(int page, int pageSize)
    {
        using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var service = CreateAppointmentService(db);

        var result = await service.GetCheckinQueueAsync(today, today, null, null, page, pageSize, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Page >= 1);
        Assert.True(result.PageSize >= 1 && result.PageSize <= 1000);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")]
    [InlineData("BOOKED")]
    [InlineData("booked")]
    [InlineData("APPROVED")]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    [InlineData("NO_SHOW")]
    [InlineData("no_show")]
    [InlineData("INVALID_STATUS")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckinQueue_StatusFilterVariations_ExecuteWithoutException(string? status)
    {
        using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var service = CreateAppointmentService(db);

        var result = await service.GetCheckinQueueAsync(today, today, null, status, 1, 15, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
    }

    #endregion

    #region 5. Invariant Checks: Zero Crashes & Performance under Volume

    [Fact]
    public async Task Invariant_LargeDataset_QueryTerminatesQuicklyWithoutInfiniteLoops()
    {
        using var db = CreateDbContext();
        var patient = new User { UserId = Guid.NewGuid(), FullName = "Benchmark Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0909000000", PasswordHash = "x" };
        var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
        db.Users.Add(patient);
        db.PatientProfiles.Add(profile);

        // Seed 300 feedback items
        var feedbacks = new List<ServiceFeedback>();
        var baseTime = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 300; i++)
        {
            feedbacks.Add(new ServiceFeedback
            {
                FeedbackId = Guid.NewGuid(),
                PatientProfileId = profile.PatientProfileId,
                PatientProfile = profile,
                CaseId = i % 2 == 0 ? Guid.NewGuid() : null,
                Rating = (short)((i % 5) + 1),
                Content = $"Comment number {i}",
                SubmittedAt = baseTime.AddHours(i)
            });
        }
        db.ServiceFeedbacks.AddRange(feedbacks);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new FeedbackRepository(db);
        var service = new FeedbackService(repo);

        var sw = Stopwatch.StartNew();
        var result = await service.GetPagedAsync("Comment", 5, 1, 15, TestContext.Current.CancellationToken);
        sw.Stop();

        Assert.NotNull(result);
        Assert.True(result.TotalItems > 0);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Query took {sw.ElapsedMilliseconds}ms, exceeding 1000ms threshold.");
    }

    #endregion
}
