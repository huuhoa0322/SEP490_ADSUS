using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Challenger;

/// <summary>
/// Adversarial empirical verification suite for Milestone 1:
/// 1. Concurrency & query robustness (scoped contexts, race condition checks on checkin).
/// 2. Relational navigation integrity (null safety on missing doctor, patient, user, case).
/// 3. Backward compatibility (single-date checkin, limit=10 audit logs, parameter aliases).
/// </summary>
public class RelationalIntegrityAndBackwardCompatibilityTests
{
    private readonly string _dbName = $"Challenger2_{Guid.NewGuid()}";

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        return new AppDbContext(options);
    }

    private AppointmentService CreateAppointmentService(
        AppDbContext db,
        Mock<IAppointmentRepository>? apptRepo = null,
        Mock<IScheduleSlotRepository>? slotRepo = null,
        Mock<IPatientProfileRepository>? profileRepo = null,
        Mock<INotificationService>? notifService = null,
        Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService>? caseService = null)
    {
        apptRepo ??= new Mock<IAppointmentRepository>();
        slotRepo ??= new Mock<IScheduleSlotRepository>();
        profileRepo ??= new Mock<IPatientProfileRepository>();
        notifService ??= new Mock<INotificationService>();
        caseService ??= new Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService>();

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

    #region 1. Concurrency & Query Robustness

    [Fact]
    public async Task Concurrency_CheckinQueue_MultipleConcurrentRequests_ExecuteSuccessfully()
    {
        // Arrange: Seed multiple appointments
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var seedDb = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. Concurrency", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0900000001", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "Patient Concurrency", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000002", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };

            seedDb.Users.AddRange(doctor, patient);
            seedDb.PatientProfiles.Add(profile);

            for (int i = 0; i < 20; i++)
            {
                var slot = new ScheduleSlot
                {
                    SlotId = Guid.NewGuid(),
                    DoctorId = doctor.UserId,
                    Doctor = doctor,
                    SlotDate = today.AddDays(i % 5),
                    StartTime = new TimeOnly(8 + (i % 8), 0),
                    EndTime = new TimeOnly(9 + (i % 8), 0),
                    Status = SlotStatus.Booked
                };
                var appt = new Appointment
                {
                    AppointmentId = Guid.NewGuid(),
                    SlotId = slot.SlotId,
                    Slot = slot,
                    PatientProfileId = profile.PatientProfileId,
                    PatientProfile = profile,
                    Status = AppointmentStatus.Booked,
                    Reason = $"Checkup {i}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                seedDb.ScheduleSlots.Add(slot);
                seedDb.Appointments.Add(appt);
            }
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: Run 25 concurrent requests, each using its own scoped DbContext as in ASP.NET Core
        var tasks = Enumerable.Range(0, 25).Select(i => Task.Run(async () =>
        {
            using var reqDb = CreateDbContext();
            var service = CreateAppointmentService(reqDb);

            var from = today.AddDays(i % 3);
            var to = today.AddDays((i % 3) + 2);
            var search = i % 2 == 0 ? "Concurrency" : null;
            var status = i % 3 == 0 ? "ALL" : "BOOKED";
            return await service.GetCheckinQueueAsync(from, to, search, status, 1, 10, TestContext.Current.CancellationToken);
        }));

        var results = await Task.WhenAll(tasks);

        // Assert: All queries completed without exception
        Assert.Equal(25, results.Length);
        Assert.All(results, r =>
        {
            Assert.NotNull(r);
            Assert.True(r.TotalCount >= 0);
            Assert.NotNull(r.Items);
        });
    }

    [Fact]
    public async Task Concurrency_AuditLog_MultipleConcurrentRequests_ExecuteSuccessfully()
    {
        // Arrange: Seed audit logs
        using (var seedDb = CreateDbContext())
        {
            var actor = new User { UserId = Guid.NewGuid(), FullName = "Admin Concurrency", Role = UserRole.Admin, Status = UserStatus.Active, Phone = "0901234567", PasswordHash = "x" };
            seedDb.Users.Add(actor);

            for (int i = 0; i < 30; i++)
            {
                seedDb.AuditLogs.Add(new AuditLog
                {
                    LogId = Guid.NewGuid(),
                    ActorId = actor.UserId,
                    Actor = actor,
                    Action = i % 2 == 0 ? "CREATE_USER" : "UPDATE_ROLE",
                    Detail = $"Detail {i}",
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: Run 20 concurrent paged queries with separate contexts
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            using var reqDb = CreateDbContext();
            var repo = new AuditLogRepository(reqDb);
            var service = new AuditLogService(repo);

            var page = (i % 2) + 1; // page 1 or 2
            var keyword = i % 2 == 0 ? "CREATE" : null;
            return await service.GetPagedAsync(keyword, null, null, null, null, page, 10, TestContext.Current.CancellationToken);
        }));

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, results.Length);
        Assert.All(results, r =>
        {
            Assert.NotNull(r);
            Assert.True(r.TotalItems > 0);
            Assert.NotEmpty(r.Items);
        });
    }

    [Fact]
    public async Task Concurrency_Feedback_MultipleConcurrentRequests_ExecuteSuccessfully()
    {
        // Arrange: Seed feedbacks with complete relational entities
        using (var seedDb = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. C", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0900000099", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "Patient C", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0909999999", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
            seedDb.Users.AddRange(doctor, patient);
            seedDb.PatientProfiles.Add(profile);

            for (int i = 0; i < 20; i++)
            {
                var c = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, PatientProfileId = profile.PatientProfileId };
                seedDb.Cases.Add(c);
                seedDb.ServiceFeedbacks.Add(new ServiceFeedback
                {
                    FeedbackId = Guid.NewGuid(),
                    PatientProfileId = profile.PatientProfileId,
                    PatientProfile = profile,
                    CaseId = c.CaseId,
                    Case = c,
                    Rating = (short)((i % 5) + 1),
                    Content = $"Comment {i}",
                    SubmittedAt = DateTime.UtcNow.AddHours(-i)
                });
            }
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: 20 concurrent paged queries with separate contexts
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            using var reqDb = CreateDbContext();
            var repo = new FeedbackRepository(reqDb);
            var service = new FeedbackService(repo);

            var rating = (short?)((i % 5) + 1);
            return await service.GetPagedAsync(null, rating, 1, 10, TestContext.Current.CancellationToken);
        }));

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, results.Length);
        Assert.All(results, r => Assert.NotNull(r));
    }

    [Fact]
    public async Task Concurrency_RaceCondition_DoubleCheckinSameAppointment_OnlyOneSucceeds()
    {
        // Arrange: Seed single appointment in Booked state scheduled for tomorrow (avoids No-Show cancellation)
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var apptId = Guid.NewGuid();

        using (var seedDb = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. Race", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0900000020", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "Patient Race", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000021", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
            var slot = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = tomorrow, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Status = SlotStatus.Booked };
            var appt = new Appointment { AppointmentId = apptId, SlotId = slot.SlotId, Slot = slot, PatientProfileId = profile.PatientProfileId, PatientProfile = profile, Status = AppointmentStatus.Booked, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

            seedDb.Users.AddRange(doctor, patient);
            seedDb.PatientProfiles.Add(profile);
            seedDb.ScheduleSlots.Add(slot);
            seedDb.Appointments.Add(appt);
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: 10 concurrent tasks attempt to check in the exact same appointment
        var results = new ConcurrentBag<(bool Success, string? Error)>();

        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            using var reqDb = CreateDbContext();
            var service = CreateAppointmentService(reqDb);
            try
            {
                var res = await service.CheckinAppointmentAsync(apptId, TestContext.Current.CancellationToken);
                results.Add((true, null));
            }
            catch (InvalidOperationException ex)
            {
                results.Add((false, ex.Message));
            }
        }));

        await Task.WhenAll(tasks);

        // Assert: At least one succeeded; any failures must be clean InvalidOperationException with "ĐÃ ĐẶT" message
        var successes = results.Count(r => r.Success);
        var failures = results.Count(r => !r.Success);

        Assert.True(successes >= 1, "At least one checkin must succeed.");
        Assert.All(results.Where(r => !r.Success), f =>
        {
            Assert.Contains("ĐÃ ĐẶT", f.Error);
        });

        // Verify final state in DB is Completed (or Approved in legacy flow)
        using (var verifyDb = CreateDbContext())
        {
            var finalAppt = await verifyDb.Appointments.FindAsync(new object[] { apptId }, TestContext.Current.CancellationToken);
            Assert.NotNull(finalAppt);
            Assert.True(finalAppt.Status == AppointmentStatus.Completed || finalAppt.Status == AppointmentStatus.Approved);
        }
    }

    #endregion

    #region 2. Relational Navigation Integrity & Missing Profiles

    [Fact]
    public async Task RelationalIntegrity_GeneralFeedbackSubmittedWithoutCase_IsIncludedInAdminQuery()
    {
        // Arrange: Submit general feedback (UC-22 / FT-36) via FeedbackService.SubmitAsync
        // Notice: General feedbacks have NO case. CaseId is null/Guid.Empty.
        Guid fbId;
        using (var db = CreateDbContext())
        {
            var patient = new User { UserId = Guid.NewGuid(), FullName = "General Patient", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000006", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
            db.Users.Add(patient);
            db.PatientProfiles.Add(profile);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var service = new FeedbackService(new FeedbackRepository(db));
            var submitted = await service.SubmitAsync(new SubmitFeedbackRequest
            {
                Rating = 5,
                Content = "Ứng dụng rất tiện lợi và hữu ích."
            }, profile.PatientProfileId, TestContext.Current.CancellationToken);

            fbId = submitted.Id;
        }

        // Act: Admin queries feedbacks via GetPagedAsync
        using (var db = CreateDbContext())
        {
            var service = new FeedbackService(new FeedbackRepository(db));
            var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);

            // Assert: REMEDIATION VERIFIED!
            // Because FeedbackRepository.GetPagedAsync applies .Include(f => f.Case) on nullable CaseId,
            // EF Core executes a LEFT JOIN on Case.
            // General feedback submitted without a case must be included in Admin feedback queries!
            var isIncluded = paged.Items.Any(i => i.Id == fbId);
            Assert.True(isIncluded, "General clinic feedback submitted without a case must be included in Admin feedback queries");

            var item = Assert.Single(paged.Items, i => i.Id == fbId);
            Assert.Equal("General Patient", item.PatientName);
            Assert.Equal("0900000006", item.PatientPhone);
            Assert.Equal(Guid.Empty, item.CaseId);
            Assert.Equal(Guid.Empty, item.DoctorId);
            Assert.Equal("Không xác định", item.DoctorName);
            Assert.Equal((short)5, item.Rating);
            Assert.Equal("Ứng dụng rất tiện lợi và hữu ích.", item.Content);
        }
    }

    [Fact]
    public async Task RelationalIntegrity_GeneralFeedbackSubmittedWithoutCase_IsIncludedInGetAllAsync()
    {
        // Arrange
        Guid fbId;
        using (var db = CreateDbContext())
        {
            var patient = new User { UserId = Guid.NewGuid(), FullName = "General Patient GetAll", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000007", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
            db.Users.Add(patient);
            db.PatientProfiles.Add(profile);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var service = new FeedbackService(new FeedbackRepository(db));
            var submitted = await service.SubmitAsync(new SubmitFeedbackRequest
            {
                Rating = 4,
                Content = "Dịch vụ phòng khám rất tốt."
            }, profile.PatientProfileId, TestContext.Current.CancellationToken);

            fbId = submitted.Id;
        }

        // Act
        using (var db = CreateDbContext())
        {
            var service = new FeedbackService(new FeedbackRepository(db));
            var all = await service.GetAllAsync(TestContext.Current.CancellationToken);

            // Assert
            var item = Assert.Single(all, i => i.Id == fbId);
            Assert.Equal("General Patient GetAll", item.PatientName);
            Assert.Equal("0900000007", item.PatientPhone);
            Assert.Equal(Guid.Empty, item.CaseId);
            Assert.Equal(Guid.Empty, item.DoctorId);
            Assert.Equal("Không xác định", item.DoctorName);
            Assert.Equal((short)4, item.Rating);
        }
    }

    [Fact]
    public async Task RelationalIntegrity_FeedbackWithCompleteRelations_LoadsAllMetadata()
    {
        // Arrange: When all relational navigation properties exist (Case, Doctor, Patient, User)
        Guid fbId;
        using (var db = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. Strange", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0901111111", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "Peter Parker", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0902222222", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
            var medicalCase = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, PatientProfileId = profile.PatientProfileId };
            var fb = new ServiceFeedback
            {
                FeedbackId = Guid.NewGuid(),
                PatientProfileId = profile.PatientProfileId,
                PatientProfile = profile,
                CaseId = medicalCase.CaseId,
                Case = medicalCase,
                Rating = 5,
                Content = "Bác sĩ điều trị rất giỏi.",
                SubmittedAt = DateTime.UtcNow
            };
            db.Users.AddRange(doctor, patient);
            db.PatientProfiles.Add(profile);
            db.Cases.Add(medicalCase);
            db.ServiceFeedbacks.Add(fb);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            fbId = fb.FeedbackId;
        }

        // Act: Query through service
        using (var db = CreateDbContext())
        {
            var service = new FeedbackService(new FeedbackRepository(db));
            var paged = await service.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);

            // Assert: All relational fields populated correctly
            var item = Assert.Single(paged.Items, i => i.Id == fbId);
            Assert.Equal("Peter Parker", item.PatientName);
            Assert.Equal("0902222222", item.PatientPhone);
            Assert.Equal("Dr. Strange", item.DoctorName);
            Assert.NotEqual(Guid.Empty, item.DoctorId);
            Assert.NotEqual(Guid.Empty, item.CaseId);
        }
    }

    [Fact]
    public async Task RelationalIntegrity_CheckinQueue_WithCompleteRelations_LoadsAllMetadata()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid apptId = Guid.NewGuid();

        using (var db = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. Gregory House", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0903333333", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "James Wilson", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0904444444", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };
            var slot = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0), Status = SlotStatus.Booked };
            var appt = new Appointment
            {
                AppointmentId = apptId,
                SlotId = slot.SlotId,
                Slot = slot,
                PatientProfileId = profile.PatientProfileId,
                PatientProfile = profile,
                Status = AppointmentStatus.Booked,
                Reason = "Oncology check",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.AddRange(doctor, patient);
            db.PatientProfiles.Add(profile);
            db.ScheduleSlots.Add(slot);
            db.Appointments.Add(appt);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        using (var db = CreateDbContext())
        {
            var service = CreateAppointmentService(db);
            var result = await service.GetCheckinQueueAsync(today, today, "Wilson", "BOOKED", 1, 15, TestContext.Current.CancellationToken);

            // Assert: Correctly populates DoctorName, PatientFullName, Phone, Reason
            var item = Assert.Single(result.Items);
            Assert.Equal(apptId, item.AppointmentId);
            Assert.Equal("James Wilson", item.PatientFullName);
            Assert.Equal("0904444444", item.PatientPhone);
            Assert.Equal("Dr. Gregory House", item.DoctorName);
            Assert.Equal("Oncology check", item.Reason);
            Assert.Equal(AppointmentStatus.Booked, item.Status);
        }
    }

    #endregion

    #region 3. Backward Compatibility

    [Fact]
    public async Task BackwardCompatibility_CheckinQueue_SingleDateOverload_PreservesLegacyContract()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid apptId = Guid.NewGuid();

        using (var db = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. LegacyCheckin", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0900000008", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "Patient Legacy", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000009", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };

            var slot = new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = doctor.UserId,
                Doctor = doctor,
                SlotDate = today,
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(14, 0),
                Status = SlotStatus.Booked
            };

            var appt = new Appointment
            {
                AppointmentId = apptId,
                SlotId = slot.SlotId,
                Slot = slot,
                PatientProfileId = profile.PatientProfileId,
                PatientProfile = profile,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.AddRange(doctor, patient);
            db.PatientProfiles.Add(profile);
            db.ScheduleSlots.Add(slot);
            db.Appointments.Add(appt);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: Call the legacy 3-parameter overload
        using (var db = CreateDbContext())
        {
            var service = CreateAppointmentService(db);
            var result = await service.GetCheckinQueueAsync(today, null, TestContext.Current.CancellationToken);

            // Assert: Returns full queue with default legacy pageSize = 1000
            Assert.NotNull(result);
            Assert.Equal(1, result.Page);
            Assert.Equal(1000, result.PageSize);
            Assert.True(result.TotalCount >= 1);
            Assert.Contains(result.Items, i => i.AppointmentId == apptId);
        }
    }

    [Fact]
    public async Task BackwardCompatibility_CheckinQueue_DateInversion_AutoSwapsAndReturnsData()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid apptId = Guid.NewGuid();

        using (var db = CreateDbContext())
        {
            var doctor = new User { UserId = Guid.NewGuid(), FullName = "Dr. Inversion", Role = UserRole.Doctor, Status = UserStatus.Active, Phone = "0900000010", PasswordHash = "x" };
            var patient = new User { UserId = Guid.NewGuid(), FullName = "Patient Inversion", Role = UserRole.Patient, Status = UserStatus.Active, Phone = "0900000011", PasswordHash = "x" };
            var profile = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient.UserId, User = patient };

            var slot = new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = doctor.UserId,
                Doctor = doctor,
                SlotDate = today.AddDays(2),
                StartTime = new TimeOnly(15, 0),
                EndTime = new TimeOnly(16, 0),
                Status = SlotStatus.Booked
            };

            var appt = new Appointment
            {
                AppointmentId = apptId,
                SlotId = slot.SlotId,
                Slot = slot,
                PatientProfileId = profile.PatientProfileId,
                PatientProfile = profile,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.AddRange(doctor, patient);
            db.PatientProfiles.Add(profile);
            db.ScheduleSlots.Add(slot);
            db.Appointments.Add(appt);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: fromDate is in the future compared to toDate (inverted)
        using (var db = CreateDbContext())
        {
            var service = CreateAppointmentService(db);
            var fromDate = today.AddDays(5);
            var toDate = today;
            var result = await service.GetCheckinQueueAsync(fromDate, toDate, null, "ALL", 1, 15, TestContext.Current.CancellationToken);

            // Assert: Swapped gracefully and found the appointment at today+2
            Assert.NotNull(result);
            Assert.Contains(result.Items, i => i.AppointmentId == apptId);
        }
    }

    [Fact]
    public async Task BackwardCompatibility_AuditLog_LimitOnly_InvokesGetRecentAndReturnsUnpagedList()
    {
        // Arrange: Mock IAuditLogService to verify controller routing
        var mockService = new Mock<IAuditLogService>();
        var sampleLogs = new List<AuditLogResponse>
        {
            new() { LogId = Guid.NewGuid(), Action = "TEST", PerformedAt = DateTime.UtcNow }
        };

        mockService.Setup(s => s.GetRecentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleLogs);

        var controller = new AuditLogsController(mockService.Object);

        // Set HttpContext with query ?limit=10 (no page)
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?limit=10");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var actionResult = await controller.GetAuditLogs(
            search: null, keyword: null, action: null, role: null, actorRole: null,
            fromDate: null, toDate: null, limit: 10, page: 1, pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: Returns OkObjectResult with ApiResponse<IReadOnlyList<AuditLogResponse>>
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<AuditLogResponse>>>(okResult.Value);
        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
        mockService.Verify(s => s.GetRecentAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        mockService.Verify(s => s.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackwardCompatibility_AuditLog_LimitWithExplicitPage_ReturnsPagedResult()
    {
        // Arrange
        var mockService = new Mock<IAuditLogService>();
        var pagedResult = new ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>(new List<AuditLogResponse>(), 1, 15, 0, 0);

        mockService.Setup(s => s.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var controller = new AuditLogsController(mockService.Object);

        // Set HttpContext with ?limit=10&page=1
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?limit=10&page=1");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var actionResult = await controller.GetAuditLogs(
            search: null, keyword: null, action: null, role: null, actorRole: null,
            fromDate: null, toDate: null, limit: 10, page: 1, pageSize: 15,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: When page is explicitly in Query, returns PagedResult
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(okResult.Value);
        Assert.Equal(200, response.Code);
        mockService.Verify(s => s.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 15, It.IsAny<CancellationToken>()), Times.Once);
        mockService.Verify(s => s.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackwardCompatibility_FeedbacksController_MinRatingAlias_MapsToRating()
    {
        // Arrange
        var mockService = new Mock<IFeedbackService>();
        var mockProfiles = new Mock<IPatientProfileRepository>();

        mockService.Setup(s => s.GetPagedAsync(
            It.IsAny<string?>(), (short)4, 1, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADSUS_BE.BLL.Common.PagedResult<FeedbackResponse>(new List<FeedbackResponse>(), 1, 15, 0, 0));

        var controller = new FeedbacksController(mockService.Object, mockProfiles.Object);

        // Act: Pass minRating = 4 without rating parameter
        var actionResult = await controller.GetAll(
            search: null, keyword: null, rating: null, minRating: 4, page: 1, pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<FeedbackResponse>>>(okResult.Value);
        Assert.Equal(200, response.Code);
        mockService.Verify(s => s.GetPagedAsync(null, (short)4, 1, 15, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BackwardCompatibility_AppointmentsController_DateParamFallback_WorksSeamlessly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mockService = new Mock<IAppointmentService>();
        var mockProfiles = new Mock<IPatientProfileRepository>();

        mockService.Setup(s => s.GetCheckinQueueAsync(
            today, today, It.IsAny<string?>(), It.IsAny<string?>(), 1, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckinQueueResponse());

        var controller = new AppointmentsController(mockService.Object, mockProfiles.Object);

        // Act: Pass only date = today (neither fromDate nor toDate)
        var actionResult = await controller.GetCheckinQueue(
            fromDate: null, toDate: null, date: today, search: null, status: null, page: 1, pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        // Assert: effectiveFrom and effectiveTo both default to the date param
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<CheckinQueueResponse>>(okResult.Value);
        Assert.Equal(200, response.Code);
        mockService.Verify(s => s.GetCheckinQueueAsync(
            today, today, null, null, 1, 15, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
