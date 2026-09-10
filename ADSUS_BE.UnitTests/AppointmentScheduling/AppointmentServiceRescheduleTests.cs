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
/// Comprehensive unit tests for AppointmentService.RescheduleAppointmentAsync (Milestone 3 / Milestone 1 feature).
/// Covers all 16 specific edge case scenarios and multi-party notification dispatch.
/// </summary>
public class AppointmentServiceRescheduleTests : IDisposable
{
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService> _caseService = new();
    private readonly NoShowService _noShowService;
    private readonly AppDbContext _db;
    private readonly AppointmentService _sut;

    public AppointmentServiceRescheduleTests()
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

    #region Helper Methods

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

    #endregion

    #region Scenario 1 Tests: BOOKED (Classic Reschedule)

    /// <summary>
    /// Scenario 1.1: Old appt BOOKED, AutoCheckin = false.
    /// Expected: Old appt -> CANCELLED ("Đổi lịch: {reason}"), Old slot -> OPEN.
    /// New appt -> BOOKED, New slot -> BOOKED, Case -> BOOKED.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario1_Booked_AutoCheckinFalse_Success()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.Booked);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân bận việc đột xuất",
            NewReason = "Khám định kỳ tai mũi họng",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.Equal(newSlot.SlotId, result.ScheduleSlotId);

        // Verify DB State
        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Cancelled, dbOldAppt!.Status);
        Assert.Equal("Đổi lịch: Bệnh nhân bận việc đột xuất", dbOldAppt.CancelledReason);

        var dbOldSlot = await _db.ScheduleSlots.FindAsync(new object[] { oldSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Open, dbOldSlot!.Status);

        var dbNewSlot = await _db.ScheduleSlots.FindAsync(new object[] { newSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, dbNewSlot!.Status);

        var dbNewAppt = await _db.Appointments.FindAsync(new object[] { result.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.NotNull(dbNewAppt);
        Assert.Equal(AppointmentStatus.Booked, dbNewAppt.Status);
        Assert.Equal("Khám định kỳ tai mũi họng", dbNewAppt.Reason);
        Assert.Equal(medicalCase.CaseId, dbNewAppt.CaseId);

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.Booked, dbCase!.Status);
    }

    /// <summary>
    /// Scenario 1.2: Old appt BOOKED, AutoCheckin = true.
    /// Expected: Old appt -> CANCELLED, Old slot -> OPEN.
    /// New appt -> COMPLETED, New slot -> BOOKED, Case -> IN_PROGRESS.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario1_Booked_AutoCheckinTrue_Success()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(14, 0), new TimeOnly(15, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.Booked);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân đến khám ngay tại phòng khám",
            AutoCheckin = true
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Completed, result.Status);

        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Cancelled, dbOldAppt!.Status);

        var dbOldSlot = await _db.ScheduleSlots.FindAsync(new object[] { oldSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Open, dbOldSlot!.Status);

        var dbNewSlot = await _db.ScheduleSlots.FindAsync(new object[] { newSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, dbNewSlot!.Status);

        var dbNewAppt = await _db.Appointments.FindAsync(new object[] { result.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Completed, dbNewAppt!.Status);
        Assert.Equal(oldAppt.Reason, dbNewAppt.Reason); // Preserves original reason when NewReason is null

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.InProgress, dbCase!.Status);
    }

    /// <summary>
    /// Scenario 1.3: Old appt BOOKED, Doctor changed during reschedule.
    /// Expected: Case.DoctorId transferred to new doctor ID.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario1_Booked_DoctorChanged_TransfersCaseDoctorId()
    {
        // Arrange
        var oldDoctor = CreateUser(Guid.NewGuid(), "BS Cũ Lê Văn C", UserRole.Doctor);
        var newDoctor = CreateUser(Guid.NewGuid(), "BS Mới Phạm Thị D", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Nguyễn Văn E", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(oldDoctor, oldDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(newDoctor, newDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, oldDoctor, oldDate, CaseStatus.Booked);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked, medicalCase);

        _db.Users.AddRange(oldDoctor, newDoctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Chuyển sang bác sĩ chuyên khoa phụ trách",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(newDoctor.UserId, dbCase!.DoctorId);
    }

    #endregion

    #region Scenario 2 Tests: COMPLETED / APPROVED + Case InProgress (Rebooking / Transfer)

    /// <summary>
    /// Scenario 2.1: Old COMPLETED, Case InProgress, AutoCheckin = true.
    /// Expected: Old appointment & old slot untouched. New appt COMPLETED, new slot BOOKED, Case remains IN_PROGRESS.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario2_CompletedWithInProgressCase_AutoCheckinTrue_Success()
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
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Completed, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "BS bận mổ cấp cứu, tái đặt lịch khám tiếp",
            AutoCheckin = true
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Completed, result.Status);

        // Old appointment and slot must remain untouched
        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Completed, dbOldAppt!.Status);
        Assert.Null(dbOldAppt.CancelledReason);

        var dbOldSlot = await _db.ScheduleSlots.FindAsync(new object[] { oldSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, dbOldSlot!.Status);

        var dbNewSlot = await _db.ScheduleSlots.FindAsync(new object[] { newSlot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, dbNewSlot!.Status);

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.InProgress, dbCase!.Status);
    }

    /// <summary>
    /// Scenario 2.2: Old COMPLETED, Case InProgress, AutoCheckin = false.
    /// Expected: Old appointment untouched. New appt BOOKED. Case remains IN_PROGRESS (NOT downgraded to Booked).
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario2_CompletedWithInProgressCase_AutoCheckinFalse_PreservesInProgressCase()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.InProgress);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Completed, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân quay lại khám ngày mai",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);

        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Completed, dbOldAppt!.Status);

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.InProgress, dbCase!.Status); // Crucial domain invariant: NOT downgraded!
    }

    /// <summary>
    /// Scenario 2.3: Old COMPLETED, Case Confirmed.
    /// Expected: Throws InvalidOperationException ("Ca đã kết thúc").
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario2_CompletedWithConfirmedCase_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.Confirmed);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Completed, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi lịch sau khi bác sĩ đã kết luận",
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("kết thúc", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario 2.4: Old COMPLETED, Case End.
    /// Expected: Throws InvalidOperationException ("Ca đã kết thúc").
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario2_CompletedWithEndCase_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.End);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Completed, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi lịch sau khi đã xuất đơn thuốc",
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("kết thúc", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Scenario 3 Tests: CANCELLED / NO_SHOW (Restore Appointment & Case)

    /// <summary>
    /// Scenario 3.1: Old CANCELLED, Case Cancelled, AutoCheckin = false.
    /// Expected: Old appt untouched. New appt BOOKED. Case restored to BOOKED.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario3_CancelledWithCancelledCase_AutoCheckinFalse_RestoresCaseToBooked()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Open);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.Cancelled);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Cancelled, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân quay lại sau khi đã huỷ trước đó",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);

        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Cancelled, dbOldAppt!.Status);

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.Booked, dbCase!.Status);
    }

    /// <summary>
    /// Scenario 3.2: Old CANCELLED, Case Cancelled, AutoCheckin = true.
    /// Expected: Old appt untouched. New appt COMPLETED. Case restored to IN_PROGRESS.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario3_CancelledWithCancelledCase_AutoCheckinTrue_RestoresCaseToInProgress()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Open);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.Cancelled);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Cancelled, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân đến trực tiếp và checkin ngay",
            AutoCheckin = true
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Completed, result.Status);

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.InProgress, dbCase!.Status);
    }

    /// <summary>
    /// Scenario 3.3: Old NO_SHOW, Case Cancelled.
    /// Expected: Old appt unchanged. New appt created, Case restored.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario3_NoShowWithCancelledCase_RestoresCaseSuccessfully()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Open);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(15, 0), new TimeOnly(16, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, doctor, oldDate, CaseStatus.Cancelled);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.NoShow, medicalCase);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bệnh nhân lỡ giờ hẹn trước do kẹt xe, xếp lịch mới",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);

        var dbOldAppt = await _db.Appointments.FindAsync(new object[] { oldAppt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, dbOldAppt!.Status);

        var dbCase = await _db.Cases.FindAsync(new object[] { medicalCase.CaseId }, TestContext.Current.CancellationToken);
        Assert.Equal(CaseStatus.Booked, dbCase!.Status);
    }

    /// <summary>
    /// Scenario 3.4: Old CANCELLED without Case (CaseId == null).
    /// Expected: New appt created with CaseId = null.
    /// </summary>
    [Fact]
    public async Task Reschedule_Scenario3_CancelledWithoutCase_CreatesAppointmentSuccessfully()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Open);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(15, 0), new TimeOnly(16, 0), SlotStatus.Open);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Cancelled, medicalCase: null);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đặt lại lịch khám mới",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var dbNewAppt = await _db.Appointments.FindAsync(new object[] { result.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Null(dbNewAppt!.CaseId);
    }

    #endregion

    #region Validation & Boundary Edge Cases

    /// <summary>
    /// Boundary 1: Target slot status != Open.
    /// Expected: Throws InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task Reschedule_NewSlotNotOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Trần Thị B", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(doctor, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(doctor, newDate, new TimeOnly(10, 0), new TimeOnly(11, 0), SlotStatus.Booked); // Slot is already Booked
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked);

        _db.Users.AddRange(doctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi lịch sang slot đã bị khoá",
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("không còn nhận đặt lịch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Boundary 2: Target slot already has Booked appointment in navigation collection.
    /// Expected: Throws InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task Reschedule_NewSlotAlreadyHasBookedAppointment_ThrowsInvalidOperationException()
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
        var existingBookedAppt = CreateAppointment(newSlot, profile2, AppointmentStatus.Booked);

        _db.Users.AddRange(doctor, patient1, patient2);
        _db.PatientProfiles.AddRange(profile1, profile2);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Appointments.AddRange(oldAppt, existingBookedAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Đổi lịch vào slot đã có người đặt",
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("đã có người đặt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Boundary 3: Same patient already has another Booked appointment on new slot's date.
    /// Expected: Throws InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task Reschedule_SamePatientSameDayBookingConflict_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor1 = CreateUser(Guid.NewGuid(), "BS Nguyễn Văn A", UserRole.Doctor);
        var doctor2 = CreateUser(Guid.NewGuid(), "BS Trần Văn B", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Lê Thị C", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));

        var oldSlot = CreateSlot(doctor1, oldDate, new TimeOnly(8, 0), new TimeOnly(9, 0), SlotStatus.Booked);
        var targetSlot = CreateSlot(doctor1, targetDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Open);
        var conflictingSlot = CreateSlot(doctor2, targetDate, new TimeOnly(14, 0), new TimeOnly(15, 0), SlotStatus.Booked);

        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked);
        var conflictingAppt = CreateAppointment(conflictingSlot, profile, AppointmentStatus.Booked);

        _db.Users.AddRange(doctor1, doctor2, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, targetSlot, conflictingSlot);
        _db.Appointments.AddRange(oldAppt, conflictingAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = targetSlot.SlotId,
            RescheduleReason = "Đổi lịch vào ngày đã có lịch hẹn khác",
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("đã có lịch khám", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Boundary 4: RescheduleReason is empty or whitespace.
    /// Expected: Throws InvalidOperationException.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Reschedule_EmptyReason_ThrowsInvalidOperationException(string? emptyReason)
    {
        // Arrange
        var appointmentId = Guid.NewGuid();
        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = emptyReason!,
            AutoCheckin = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RescheduleAppointmentAsync(appointmentId, request, TestContext.Current.CancellationToken));
        Assert.Contains("Lý do đổi lịch là bắt buộc", ex.Message);
    }

    /// <summary>
    /// Boundary 5: Old appointment ID does not exist in DB.
    /// Expected: Throws KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task Reschedule_OldAppointmentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Lý do hợp lệ",
            AutoCheckin = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.RescheduleAppointmentAsync(nonExistentId, request, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Notifications Test

    /// <summary>
    /// Notification Verification: Dispatched post-commit to Patient, New Doctor, and Old Doctor.
    /// </summary>
    [Fact]
    public async Task Reschedule_Notifications_DispatchedToPatientAndBothDoctors()
    {
        // Arrange
        var oldDoctor = CreateUser(Guid.NewGuid(), "BS Cũ Lê Văn C", UserRole.Doctor);
        var newDoctor = CreateUser(Guid.NewGuid(), "BS Mới Phạm Thị D", UserRole.Doctor);
        var patient = CreateUser(Guid.NewGuid(), "Nguyễn Văn E", UserRole.Patient);
        var profile = CreatePatientProfile(patient);

        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var oldSlot = CreateSlot(oldDoctor, oldDate, new TimeOnly(9, 0), new TimeOnly(10, 0), SlotStatus.Booked);
        var newSlot = CreateSlot(newDoctor, newDate, new TimeOnly(14, 0), new TimeOnly(15, 0), SlotStatus.Open);
        var medicalCase = CreateCase(profile, oldDoctor, oldDate, CaseStatus.Booked);
        var oldAppt = CreateAppointment(oldSlot, profile, AppointmentStatus.Booked, medicalCase);

        _db.Users.AddRange(oldDoctor, newDoctor, patient);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.AddRange(oldSlot, newSlot);
        _db.Cases.Add(medicalCase);
        _db.Appointments.Add(oldAppt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = newSlot.SlotId,
            RescheduleReason = "Bác sĩ cũ bận công tác, chuyển viện",
            AutoCheckin = false
        };

        // Act
        var result = await _sut.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);

        // 1. Patient Notification
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r =>
                r.UserId == patient.UserId &&
                r.Type == "appointment_rescheduled" &&
                r.Title != null && r.Title.Contains("đổi") &&
                r.Body != null && r.Body.Contains(newDoctor.FullName)),
            It.IsAny<CancellationToken>()), Times.Once);

        // 2. New Doctor Notification
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r =>
                r.UserId == newDoctor.UserId &&
                r.Type == "doctor_appointment_rescheduled" &&
                r.Title != null && r.Title.Contains("chuyển đến") &&
                r.Body != null && r.Body.Contains(patient.FullName)),
            It.IsAny<CancellationToken>()), Times.Once);

        // 3. Old Doctor Notification (transferred)
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r =>
                r.UserId == oldDoctor.UserId &&
                r.Type == "doctor_appointment_transferred" &&
                r.Title != null && r.Title.Contains("chuyển bác sĩ") &&
                r.Body != null && r.Body.Contains(patient.FullName) &&
                r.Body.Contains(newDoctor.FullName)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
