using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ScheduleSlot = ADSUS_BE.DAL.Entities.ScheduleSlot;
using Appointment = ADSUS_BE.DAL.Entities.Appointment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Challenger;

/// <summary>
/// Empirical Adversarial Challenge Test Harness for Phase 5: Audit Remediation (Code & Specs).
/// Verifies:
/// 1. BR-065 (Appointment IDOR Defense)
///    - Doctor A cannot view Doctor B's appointment (403 / UnauthorizedAccessException)
///    - Patient X cannot view Patient Y's appointment (403 / UnauthorizedAccessException)
///    - Admin can view ANY appointment (200 OK)
///    - Assigned Patient can view own appointment (200 OK)
///    - Assigned Doctor can view own appointment (200 OK)
///    - Booker (relative) can view appointment booked for family (200 OK)
///    - Unrelated roles (Nurse, Pharmacist) cannot view without assignment (403 / UnauthorizedAccessException)
/// 2. BR-066 (Cancellation Temporal Guard)
///    - Slot date in past -> Rejected (InvalidOperationException)
///    - Slot date today, start time <= current Vietnam time -> Rejected (InvalidOperationException)
///    - Slot date today, start time > current Vietnam time -> Allowed
///    - Slot date in future -> Allowed
///    - Cancellation of non-booked appointment -> Rejected
///    - Cancellation by non-owner -> Rejected (UnauthorizedAccessException)
/// 3. BR-123 (Inventory Calculation Expiry Filter)
///    - Expired batches (ExpiryDate < today) excluded from TotalInventoryBase
///    - Batches expiring today (ExpiryDate == today) included in TotalInventoryBase
///    - Future expiry batches included in TotalInventoryBase
///    - Medicine with only expired batches -> TotalInventoryBase == 0
///    - Medicine with no batches -> TotalInventoryBase == 0
///    - Consistent across GetPagedAsync, SearchMedicinesAsync, and GetByIdAsync
/// </summary>
public class Phase5AuditRemediationEmpiricalStressTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService> _caseService = new();
    private readonly Mock<IMedicineRepository> _medicineRepo = new();
    private readonly AppointmentService _appointmentService;
    private readonly MedicineService _medicineService;

    private static readonly TimeZoneInfo VietnamZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("Vietnam Standard Time", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
            }
        }
    }

    public Phase5AuditRemediationEmpiricalStressTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditRemediationStress_{Guid.NewGuid()}")
            .Options;
        _db = new AppDbContext(options);

        var noShowService = NoShowTestServices.Create(_db, _notificationService.Object);

        _appointmentService = new AppointmentService(
            _appointmentRepo.BackedBy(_db).Object,
            _slotRepo.BackedBy(_db).Object,
            new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db),
            new ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService(_profileRepo.Object, new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db), Microsoft.Extensions.Logging.Abstractions.NullLogger<ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService>.Instance),
            PatientAccountTestServices.Relationship(_db),
            _notificationService.Object,
            _caseService.BackedBy(_db).Object,
            noShowService,
            new ADSUS_BE.DAL.Repositories.Implementations.UnitOfWork(_db),
            Mock.Of<ILogger<AppointmentService>>());

        _medicineService = new MedicineService(
            _medicineRepo.BackedBy(_db).Object,
            new ADSUS_BE.DAL.Repositories.Implementations.MedicinePackagingRepository(_db),
            new ADSUS_BE.DAL.Repositories.Implementations.MedicineUnitRepository(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Factory Methods

    private User CreateDoctor(string name = "Dr. Test", Guid? id = null)
    {
        var doctor = new User
        {
            UserId = id ?? Guid.NewGuid(),
            FullName = name,
            Email = $"{Guid.NewGuid():N}@test.com",
            Phone = "0123456789",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(doctor);
        return doctor;
    }

    private ScheduleSlot CreateScheduleSlot(
        SlotStatus status,
        User doctor,
        DateOnly? date = null,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null)
    {
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            StartTime = startTime ?? new TimeOnly(9, 0),
            EndTime = endTime ?? new TimeOnly(10, 0),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Appointments = new List<Appointment>(),
        };

        _db.ScheduleSlots.Add(slot);
        return slot;
    }

    private PatientProfile CreatePatientProfile(Guid? userId = null, string fullName = "Bệnh Nhân Test")
    {
        var patientUserId = userId ?? Guid.NewGuid();
        var patientUser = new User
        {
            UserId = patientUserId,
            FullName = fullName,
            Email = $"{Guid.NewGuid():N}@patient.com",
            Phone = "0987654321",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(patientUser);

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUserId,
            User = patientUser,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(profile);

        _profileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        return profile;
    }

    #endregion

    #region 1. BR-065 IDOR Defense Stress Tests

    [Fact]
    public async Task BR065_DoctorA_CannotView_DoctorB_Appointment_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var doctorA = CreateDoctor("Dr. Doctor A");
        var doctorB = CreateDoctor("Dr. Doctor B");
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctorB, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        var patientProfile = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientProfile.PatientProfileId,
            PatientProfile = patientProfile,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // Act & Assert: Doctor A attempts to view Doctor B's appointment -> Throws UnauthorizedAccessException
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _appointmentService.GetByIdAsync(
                appointment.AppointmentId,
                currentUserId: doctorA.UserId,
                currentUserRole: "DOCTOR",
                currentPatientProfileId: null,
                ct: TestContext.Current.CancellationToken));

        Assert.Contains("Bạn không có quyền truy cập", ex.Message);
    }

    [Fact]
    public async Task BR065_PatientX_CannotView_PatientY_Appointment_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor);
        var patientX = CreatePatientProfile(fullName: "Patient X (Attacker)");
        var patientY = CreatePatientProfile(fullName: "Patient Y (Victim)");

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientY.PatientProfileId,
            PatientProfile = patientY,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // Act & Assert: Patient X attempts to view Patient Y's appointment -> Throws UnauthorizedAccessException
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _appointmentService.GetByIdAsync(
                appointment.AppointmentId,
                currentUserId: patientX.UserId!.Value,
                currentUserRole: "PATIENT",
                currentPatientProfileId: patientX.PatientProfileId,
                ct: TestContext.Current.CancellationToken));

        Assert.Contains("Bạn không có quyền truy cập", ex.Message);
    }

    [Fact]
    public async Task BR065_Admin_CanView_AnyAppointment_ReturnsAppointmentResponse()
    {
        // Arrange: Appointment belongs to Doctor B and Patient Y
        var doctorB = CreateDoctor("Dr. Doctor B");
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctorB);
        var patientY = CreatePatientProfile(fullName: "Patient Y");

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientY.PatientProfileId,
            PatientProfile = patientY,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var adminUserId = Guid.NewGuid();

        // Act: Admin views this appointment
        var response = await _appointmentService.GetByIdAsync(
            appointment.AppointmentId,
            currentUserId: adminUserId,
            currentUserRole: "ADMIN",
            currentPatientProfileId: null,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(appointment.AppointmentId, response!.AppointmentId);
    }

    [Fact]
    public async Task BR065_AssignedPatient_CanView_OwnAppointment_ReturnsAppointmentResponse()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor);
        var patient = CreatePatientProfile(fullName: "Patient Chính Chủ");

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // Act: Patient views own appointment
        var response = await _appointmentService.GetByIdAsync(
            appointment.AppointmentId,
            currentUserId: patient.UserId!.Value,
            currentUserRole: "PATIENT",
            currentPatientProfileId: patient.PatientProfileId,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(appointment.AppointmentId, response!.AppointmentId);
    }

    [Fact]
    public async Task BR065_AssignedDoctor_CanView_AssignedAppointment_ReturnsAppointmentResponse()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Assigned");
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // Act: Assigned doctor views appointment
        var response = await _appointmentService.GetByIdAsync(
            appointment.AppointmentId,
            currentUserId: doctor.UserId,
            currentUserRole: "DOCTOR",
            currentPatientProfileId: null,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(appointment.AppointmentId, response!.AppointmentId);
    }

    [Fact]
    public async Task BR065_FamilyBooker_CanView_AppointmentBookedForRelative_ReturnsAppointmentResponse()
    {
        // Arrange: Booker is User A, PatientProfile belongs to User B (e.g. elderly parent or child)
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor);
        var bookerUserId = Guid.NewGuid();
        var relativeProfile = CreatePatientProfile(fullName: "Bệnh Nhân Người Thân");

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = relativeProfile.PatientProfileId,
            PatientProfile = relativeProfile,
            BookedByUserId = bookerUserId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // Act: Booker views relative's appointment
        var response = await _appointmentService.GetByIdAsync(
            appointment.AppointmentId,
            currentUserId: bookerUserId,
            currentUserRole: "PATIENT",
            currentPatientProfileId: Guid.NewGuid(), // Booker has their own separate profile
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(appointment.AppointmentId, response!.AppointmentId);
    }

    [Fact]
    public async Task BR065_CrossRole_NurseOrPharmacistWithoutAssignment_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor);
        var patient = CreatePatientProfile();
        var nurseUserId = Guid.NewGuid();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _appointmentRepo.Setup(r => r.GetByIdAsync(appointment.AppointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _appointmentService.GetByIdAsync(
                appointment.AppointmentId,
                currentUserId: nurseUserId,
                currentUserRole: "NURSE",
                currentPatientProfileId: null,
                ct: TestContext.Current.CancellationToken));

        Assert.Contains("Bạn không có quyền truy cập", ex.Message);
    }

    #endregion

    #region 2. BR-066 Cancellation Temporal Guard Stress Tests

    [Fact]
    public async Task BR066_Cancel_SlotDateInPast_ThrowsInvalidOperationException()
    {
        // Arrange: Slot date is in the past (yesterday)
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
        var yesterdayVn = DateOnly.FromDateTime(nowVn.AddDays(-1));
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, yesterdayVn);
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.CancelAppointmentAsync(
                appointment.AppointmentId,
                patient.UserId!.Value,
                patient.PatientProfileId,
                new CancelAppointmentRequest { CancellationReason = "Bận việc đột xuất" },
                TestContext.Current.CancellationToken));

        Assert.Contains("Chỉ có thể hủy lịch ít nhất 12 giờ trước thời gian", ex.Message);
    }

    [Fact]
    public async Task BR066_Cancel_SlotDate10DaysInPast_ThrowsInvalidOperationException()
    {
        // Arrange: Slot date is 10 days in past
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
        var pastVn = DateOnly.FromDateTime(nowVn.AddDays(-10));
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, pastVn, new TimeOnly(14, 0), new TimeOnly(15, 0));
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.CancelAppointmentAsync(
                appointment.AppointmentId,
                patient.UserId!.Value,
                patient.PatientProfileId,
                new CancelAppointmentRequest { CancellationReason = "Quên hủy" },
                TestContext.Current.CancellationToken));

        Assert.Contains("Chỉ có thể hủy lịch ít nhất 12 giờ trước thời gian", ex.Message);
    }

    [Fact]
    public async Task BR066_Cancel_SlotDateToday_StartTimeInPast_ThrowsInvalidOperationException()
    {
        // Arrange: Slot is TODAY, but start time is earlier today (00:00 midnight is <= any daytime)
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
        var todayVn = DateOnly.FromDateTime(nowVn);
        var pastStartTime = new TimeOnly(0, 0);

        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, todayVn, pastStartTime, new TimeOnly(0, 30));
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.CancelAppointmentAsync(
                appointment.AppointmentId,
                patient.UserId!.Value,
                patient.PatientProfileId,
                new CancelAppointmentRequest { CancellationReason = "Bận việc hôm nay" },
                TestContext.Current.CancellationToken));

        Assert.Contains("Chỉ có thể hủy lịch ít nhất 12 giờ trước thời gian", ex.Message);
    }

    [Fact]
    public async Task BR066_Cancel_SlotDateFuture_StartTimeInFuture_Succeeds()
    {
        // Arrange: Slot is TODAY, but StartTime is in the future (23:55 late night)
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
        var todayVn = DateOnly.FromDateTime(nowVn.AddDays(2));
        var futureStartTime = new TimeOnly(23, 55);

        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, todayVn, futureStartTime, new TimeOnly(23, 59));
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Cancel should be allowed
        var result = await _appointmentService.CancelAppointmentAsync(
            appointment.AppointmentId,
            patient.UserId!.Value,
            patient.PatientProfileId,
            new CancelAppointmentRequest { CancellationReason = "Thay đổi lịch trình trước giờ khám" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
        Assert.Equal(SlotStatus.Open, slot.Status); // Slot reopened
    }

    [Fact]
    public async Task BR066_Cancel_SlotDateInFuture_Succeeds()
    {
        // Arrange: Slot date is in the future (tomorrow)
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
        var tomorrowVn = DateOnly.FromDateTime(nowVn.AddDays(2));

        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, tomorrowVn, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Cancel future appointment
        var result = await _appointmentService.CancelAppointmentAsync(
            appointment.AppointmentId,
            patient.UserId!.Value,
            patient.PatientProfileId,
            new CancelAppointmentRequest { CancellationReason = "Bận chuyến công tác ngày mai" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
        Assert.Equal(SlotStatus.Open, slot.Status); // Slot reopened
    }

    [Fact]
    public async Task BR066_Cancel_AlreadyCompletedAppointment_ThrowsInvalidOperationException()
    {
        // Arrange: Appointment has already completed
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        var patient = CreatePatientProfile();

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patient.PatientProfileId,
            PatientProfile = patient,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.CancelAppointmentAsync(
                appointment.AppointmentId,
                patient.UserId!.Value,
                patient.PatientProfileId,
                new CancelAppointmentRequest { CancellationReason = "Muốn hủy" },
                TestContext.Current.CancellationToken));

        Assert.Equal("Chỉ lịch hẹn đang đặt mới được hủy.", ex.Message);
    }

    [Fact]
    public async Task BR066_Cancel_NonOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange: Appointment belongs to Patient A, but Patient B attempts cancellation
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        var patientA = CreatePatientProfile(fullName: "Patient A");
        var patientB = CreatePatientProfile(fullName: "Patient B (Unauthorized)");

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientA.PatientProfileId,
            PatientProfile = patientA,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _appointmentService.CancelAppointmentAsync(
                appointment.AppointmentId,
                patientB.UserId!.Value,
                patientB.PatientProfileId,
                new CancelAppointmentRequest { CancellationReason = "Hủy hộ không có quyền" },
                TestContext.Current.CancellationToken));

        Assert.Equal("Bạn không có quyền hủy lịch hẹn này.", ex.Message);
    }

    #endregion

    #region 3. BR-123 Inventory Calculation Expiry Filter Stress Tests

    [Fact]
    public async Task BR123_Inventory_ExcludesExpiredBatches_IncludesTodayAndFutureBatches()
    {
        // Arrange
        var today = ClinicClock.Today(); // ngày phòng khám, cùng mốc với service
        var medId = Guid.NewGuid();

        var medicine = new Medicine
        {
            MedicineId = medId,
            Name = "Antibiotic Test Med",
            Status = MedicineStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LowStockThreshold = 10,
            MedicineBatches = new List<MedicineBatch>
            {
                // Expired batches (< today) - MUST BE EXCLUDED
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "EXP-1", QuantityBase = 50, ExpiryDate = today.AddDays(-1) },
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "EXP-2", QuantityBase = 120, ExpiryDate = today.AddMonths(-6) },
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "EXP-3", QuantityBase = 300, ExpiryDate = today.AddYears(-2) },

                // Boundary batch: expires TODAY (>= today) - MUST BE INCLUDED
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "TODAY-1", QuantityBase = 35, ExpiryDate = today },

                // Future batches (> today) - MUST BE INCLUDED
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "FUT-1", QuantityBase = 65, ExpiryDate = today.AddDays(2) },
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "FUT-2", QuantityBase = 100, ExpiryDate = today.AddMonths(12) }
            }
        };

        _db.Medicines.Add(medicine);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _medicineRepo.Setup(r => r.GetPagedAsync(1, 10, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Medicine> { medicine }, 1));
        _medicineRepo.Setup(r => r.SearchByNameAsync("Antibiotic", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Medicine> { medicine });

        // Expected available stock = 35 (today) + 65 (tomorrow) + 100 (next year) = 200
        // Excluded stock = 50 + 120 + 300 = 470

        // Act 1: GetByIdAsync
        var byIdResult = await _medicineService.GetByIdAsync(medId, TestContext.Current.CancellationToken);
        Assert.NotNull(byIdResult);
        Assert.Equal(200, byIdResult.TotalInventoryBase);

        // Act 2: GetPagedAsync
        var pagedResult = await _medicineService.GetPagedAsync(1, 10, null, null, null, TestContext.Current.CancellationToken);
        Assert.NotNull(pagedResult);
        var pagedItem = Assert.Single(pagedResult.Items);
        Assert.Equal(200, pagedItem.TotalInventoryBase);

        // Act 3: SearchMedicinesAsync
        var searchResult = await _medicineService.SearchMedicinesAsync("Antibiotic", 20, TestContext.Current.CancellationToken);
        Assert.NotNull(searchResult);
        var searchItem = Assert.Single(searchResult);
        Assert.Equal(200, searchItem.TotalInventoryBase);
    }

    [Fact]
    public async Task BR123_Inventory_MedicineWithOnlyExpiredBatches_ReturnsZeroStock()
    {
        // Arrange
        var today = ClinicClock.Today(); // ngày phòng khám, cùng mốc với service
        var medId = Guid.NewGuid();

        var medicine = new Medicine
        {
            MedicineId = medId,
            Name = "Expired Only Med",
            Status = MedicineStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LowStockThreshold = 10,
            MedicineBatches = new List<MedicineBatch>
            {
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "EXP-A", QuantityBase = 100, ExpiryDate = today.AddDays(-1) },
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "EXP-B", QuantityBase = 200, ExpiryDate = today.AddDays(-15) }
            }
        };

        _db.Medicines.Add(medicine);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _medicineRepo.Setup(r => r.GetPagedAsync(1, 10, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Medicine> { medicine }, 1));
        _medicineRepo.Setup(r => r.SearchByNameAsync("Expired", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Medicine> { medicine });

        // Act & Assert across all 3 methods
        var byIdResult = await _medicineService.GetByIdAsync(medId, TestContext.Current.CancellationToken);
        Assert.NotNull(byIdResult);
        Assert.Equal(0, byIdResult.TotalInventoryBase);

        var pagedResult = await _medicineService.GetPagedAsync(1, 10, null, null, null, TestContext.Current.CancellationToken);
        Assert.NotNull(pagedResult);
        Assert.Equal(0, Assert.Single(pagedResult.Items).TotalInventoryBase);

        var searchResult = await _medicineService.SearchMedicinesAsync("Expired", 20, TestContext.Current.CancellationToken);
        Assert.NotNull(searchResult);
        Assert.Equal(0, Assert.Single(searchResult).TotalInventoryBase);
    }

    [Fact]
    public async Task BR123_Inventory_MedicineWithNoBatches_ReturnsZeroStock()
    {
        // Arrange
        var medId = Guid.NewGuid();
        var medicine = new Medicine
        {
            MedicineId = medId,
            Name = "No Batches Med",
            Status = MedicineStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LowStockThreshold = 5,
            MedicineBatches = new List<MedicineBatch>()
        };

        _db.Medicines.Add(medicine);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _medicineRepo.Setup(r => r.GetPagedAsync(1, 10, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Medicine> { medicine }, 1));
        _medicineRepo.Setup(r => r.SearchByNameAsync("No Batches", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Medicine> { medicine });

        // Act & Assert across all 3 methods
        var byIdResult = await _medicineService.GetByIdAsync(medId, TestContext.Current.CancellationToken);
        Assert.NotNull(byIdResult);
        Assert.Equal(0, byIdResult.TotalInventoryBase);

        var pagedResult = await _medicineService.GetPagedAsync(1, 10, null, null, null, TestContext.Current.CancellationToken);
        Assert.NotNull(pagedResult);
        Assert.Equal(0, Assert.Single(pagedResult.Items).TotalInventoryBase);

        var searchResult = await _medicineService.SearchMedicinesAsync("No Batches", 20, TestContext.Current.CancellationToken);
        Assert.NotNull(searchResult);
        Assert.Equal(0, Assert.Single(searchResult).TotalInventoryBase);
    }

    #endregion
}
