using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common.DTOs;
using ADSUS_BE.BLL.Common.Interfaces;
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
/// Adversarial stress tests authored by Challenger 1 for AppointmentService.RescheduleAppointmentAsync.
/// Challenges boundary assumptions, notification fault tolerance, same-day slot switching,
/// null safety, doctor transfer conditionals, and slot appointment status filters.
/// </summary>
public class AppointmentServiceRescheduleAdversarialTests : IDisposable
{
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService> _caseService = new();
    private readonly NoShowService _noShowService;
    private readonly AppDbContext _db;
    private readonly AppointmentService _sut;

    public AppointmentServiceRescheduleAdversarialTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var noShowSettings = Options.Create(new ADSUS_BE.BLL.Common.Settings.NoShowSettings { GraceTimeMinutes = 15 });
        _noShowService = new NoShowService(
            _db,
            noShowSettings,
            _notificationService.Object,
            _profileRepo.Object,
            Mock.Of<ILogger<NoShowService>>());

        _sut = new AppointmentService(
            _appointmentRepo.Object,
            _slotRepo.Object,
            _profileRepo.Object,
            _notificationService.Object,
            _caseService.Object,
            _noShowService,
            _db,
            Mock.Of<ILogger<AppointmentService>>());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private User CreateUser(Guid userId, string name, UserRole role, string phone = "0900000001")
    {
        return new User
        {
            UserId = userId,
            FullName = name,
            Email = $"{name.ToLower().Replace(" ", "")}@adsus.test",
            Phone = phone,
            PasswordHash = "hash",
            Status = UserStatus.Active,
            Role = role,
        };
    }

    private PatientProfile CreatePatientProfile(User user)
    {
        return new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            Gender = GenderType.Female,
            CreatedBy = Guid.NewGuid(),
        };
    }

    private ScheduleSlot CreateSlot(User doctor, DateOnly date, TimeOnly start, TimeOnly end, SlotStatus status = SlotStatus.Open)
    {
        return new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = date,
            StartTime = start,
            EndTime = end,
            Status = status,
        };
    }

    private Case CreateCase(PatientProfile profile, User doctor, DateOnly visitDate, CaseStatus status = CaseStatus.Booked)
    {
        return new Case
        {
            CaseId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            DoctorId = doctor.UserId,
            Doctor = doctor,
            VisitDate = visitDate,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private Appointment CreateAppointment(
        ScheduleSlot slot,
        PatientProfile profile,
        AppointmentStatus status,
        Case? medicalCase = null,
        string reason = "Khám tổng quát")
    {
        return new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Status = status,
            Reason = reason,
            CaseId = medicalCase?.CaseId,
            Case = medicalCase,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Adversarial Challenge 1: Notification Gateway Failure Isolation
    /// When notification service throws unhandled exception for patient, new doctor, and old doctor,
    /// the database transaction must still commit and the method must return the created appointment.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_NotificationThrowsException_RescheduleStillSucceedsAndCommits()
    {
        // Arrange
        var oldDoctor = CreateUser(Guid.NewGuid(), "BS Cũ", UserRole.Doctor);
        var newDoctor = CreateUser(Guid.NewGuid(), "BS Mới", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Bệnh nhân", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(oldDoctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(newDoctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked);

        _db.Users.AddRange(oldDoctor, newDoctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Configure notification service to throw for ALL calls
        _notificationService
            .Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Firebase notification network connection timeout."));

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi lịch khi mạng notification bị lỗi",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);

        // DB State must be committed
        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Cancelled, dbOldAppt!.Status);

        var dbNewSlot = await _db.ScheduleSlots.FindAsync(new object[] { newSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, dbNewSlot!.Status);
    }

    /// <summary>
    /// Adversarial Challenge 2: Same-Day Reschedule (Time slot switch on same day)
    /// A patient wants to reschedule from 08:00 to 14:00 on the SAME date.
    /// The conflict checker must not falsely flag the old appointment as a collision.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_SameDayRescheduleToAnotherSlotOnSameDay_Succeeds()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var sameDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));

        var morningSlot = CreateSlot(doctor, sameDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var afternoonSlot = CreateSlot(doctor, sameDate, new TimeOnly(14, 0), new TimeOnly(15, 0), SlotStatus.Open);
        var oldAppt = CreateAppointment(morningSlot, profile, AppointmentStatus.Booked);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(morningSlot, afternoonSlot);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = afternoonSlot.SlotId,
            RescheduleReason = "Dời từ sáng sang chiều cùng ngày",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.Equal(afternoonSlot.SlotId, result.ScheduleSlotId);
    }

    /// <summary>
    /// Adversarial Challenge 3: Same-Day Conflict when patient has a distinct 2nd active booking
    /// If the patient already has ANOTHER booked appointment on the target date,
    /// rescheduling must be blocked.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_SameDayConflict_WhenPatientHasSecondActiveBooking_Throws()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var targetSlot = CreateSlot(doctor, targetDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var existingOtherSlot = CreateSlot(doctor, targetDate, new TimeOnly(15, 0), new TimeOnly(16, 0), SlotStatus.Booked);

        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked);
        var existingOtherAppt = CreateAppointment(existingOtherSlot, profile, AppointmentStatus.Booked);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, targetSlot, existingOtherSlot);
        _db.Appointments.AddRange(oldAppt, existingOtherAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = targetSlot.SlotId,
            RescheduleReason = "Đổi lịch sang ngày đã có một ca khám khác",
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("đã có lịch khám", ex.Message);
    }

    /// <summary>
    /// Adversarial Challenge 4: Scenario 2 with APPROVED Status (Backward compatibility)
    /// Appointments with status APPROVED and Case InProgress must be treated as Scenario 2.
    /// Old appointment preserved, new appointment created.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_Scenario2_ApprovedStatus_Success()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(14, 0), new TimeOnly(15, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.InProgress);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Approved, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân cần khám lại ở ca APPROVED",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);

        // Old appointment remains APPROVED
        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Approved, dbOldAppt!.Status);

        // Case remains InProgress
        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.InProgress, dbCase!.Status);
    }

    /// <summary>
    /// Adversarial Challenge 5: Null Request Guard
    /// Passing null request must throw ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.RescheduleAppointmentAsync(Guid.NewGuid(), null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Adversarial Challenge 6: Same Doctor Transfer Notification Suppression
    /// When rescheduling with the SAME doctor, old doctor notification must NOT be dispatched.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_SameDoctor_SuppressesOldDoctorTransferredNotification()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi giờ với cùng bác sĩ",
            AutoCheckin = false
        };

        // Act
        await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert: Old doctor transfer notification was NEVER sent
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r => r.Type == "doctor_appointment_transferred"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Adversarial Challenge 7: Target Slot with Cancelled Appointment
    /// If an open slot previously had a cancelled appointment in its collection,
    /// it must not prevent rescheduling to that slot.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_TargetSlotWithCancelledAppointment_Succeeds()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient1 = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var patient2 = CreateUser(Guid.NewGuid(), "Lê Văn C", UserRole.Patient);
        var profile1 = CreatePatientProfile(patient1);
        var profile2 = CreatePatientProfile(patient2);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);

        var oldAppt = CreateAppointment(oldSlot, profile1, AppointmentStatus.Booked);
        var cancelledApptOnNewSlot = CreateAppointment(newSlot, profile2, AppointmentStatus.Cancelled);

        _db.Users.AddRange(doctor, patient1, patient2);
        _db.PatientProfiles.AddRange(profile1, profile2);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.AddRange(oldAppt, cancelledApptOnNewSlot);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi sang slot đã từng có lịch hủy",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    /// <summary>
    /// Adversarial Challenge 8: Null Case Safety
    /// When old appointment has no associated Case (Case is null),
    /// rescheduling must proceed smoothly without NullReferenceException.
    /// </summary>
    [Fact]
    public async Task Reschedule_Adversarial_NullCase_SucceedsWithoutNullReference()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked, medicalCase: null);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi lịch cho ca không có Medical Case",
            AutoCheckin = true
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Completed, result.Status);
        Assert.Null(result.CaseId);
    }
}
