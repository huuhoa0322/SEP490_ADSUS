using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
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
/// Unit tests for AppointmentService.CheckinAppointmentAsync (Module 8 - Nurse Checkin).
/// Flow: Booked (patient đặt lịch) → Approved (nurse checkin khi bệnh nhân đến)
/// </summary>
public class AppointmentServiceCheckinTests : IDisposable
{
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService> _caseService = new();
    private readonly NoShowService _noShowService;
    private readonly AppDbContext _db;
    private readonly AppointmentService _sut;

    // Test data
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _slotId = Guid.NewGuid();
    private readonly Guid _appointmentId = Guid.NewGuid();

    public AppointmentServiceCheckinTests()
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
    }

    #region Helper Methods

    private User CreateDoctor(string name = "Dr. Test")
    {
        return new User
        {
            UserId = _doctorId,
            Phone = "0900000001",
            FullName = name,
            Email = $"{name.ToLower().Replace(" ", "")}@test.com",
            PasswordHash = "hash",
            Status = UserStatus.Active,
            Role = UserRole.Doctor,
        };
    }

    private User CreatePatient()
    {
        return new User
        {
            UserId = _patientId,
            Phone = "0900000002",
            FullName = "Patient Test",
            Email = "patient@test.com",
            PasswordHash = "hash",
            Status = UserStatus.Active,
            Role = UserRole.Patient,
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

    private ScheduleSlot CreateSlot(User doctor, SlotStatus status = SlotStatus.Open)
    {
        return new ScheduleSlot
        {
            SlotId = _slotId,
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = status,
        };
    }

    private Appointment CreateAppointment(ScheduleSlot slot, PatientProfile profile, AppointmentStatus status)
    {
        return new Appointment
        {
            AppointmentId = _appointmentId,
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Status = status,
            Reason = "Regular checkup",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private async Task<Appointment> SeedAppointmentAsync(Appointment appointment)
    {
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return appointment;
    }

    #endregion

    #region TC-001: Valid Booked Appointment Checkin

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-001
    /// Happy path: Appointment đang Booked → Checkin → Status chuyển thành Approved
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_ValidBookedAppointment_StatusChangesToApproved()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.CheckinAppointmentAsync(_appointmentId);

        // Assert
        Assert.Equal(AppointmentStatus.Approved, result.Status);

        // Verify DB was updated
        var updatedAppointment = await _db.Appointments.FindAsync(_appointmentId);
        Assert.Equal(AppointmentStatus.Approved, updatedAppointment!.Status);
    }

    #endregion

    #region TC-002: Already Approved Appointment

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-002
    /// Edge case: Appointment đã Approved (đã checkin) → Checkin lại → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_AlreadyApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Approved);

        await SeedAppointmentAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinAppointmentAsync(_appointmentId));

        Assert.Contains("ĐÃ ĐẶT", ex.Message);
    }

    #endregion

    #region TC-003: Cancelled Appointment

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-003
    /// Edge case: Appointment đã Cancelled → Checkin → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_CancelledAppointment_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Cancelled);

        await SeedAppointmentAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinAppointmentAsync(_appointmentId));

        Assert.Contains("ĐÃ ĐẶT", ex.Message);
    }

    #endregion

    #region TC-004: Completed Appointment

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-004
    /// Edge case: Appointment đã Completed (bác sĩ đã kết thúc ca) → Checkin → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_CompletedAppointment_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Completed);

        await SeedAppointmentAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinAppointmentAsync(_appointmentId));

        Assert.Contains("ĐÃ ĐẶT", ex.Message);
    }

    #endregion

    #region TC-005: Appointment Not Found

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-005
    /// Edge case: Appointment không tồn tại → Checkin → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_AppointmentNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinAppointmentAsync(nonExistentId));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region TC-006: Response Contains Correct Data

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-006
    /// Verify: Checkin thành công → Response chứa đúng thông tin appointment
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_ValidCheckin_ReturnsAppointmentResponse()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Smith");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        slot.SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        slot.StartTime = new TimeOnly(14, 0);
        slot.EndTime = new TimeOnly(15, 0);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);
        appointment.Reason = "Follow-up visit";

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.CheckinAppointmentAsync(_appointmentId);

        // Assert
        Assert.Equal(_appointmentId, result.AppointmentId);
        Assert.Equal(_slotId, result.ScheduleSlotId);
        Assert.Equal(slot.SlotDate, result.SlotDate);
        Assert.Equal(slot.StartTime, result.StartTime);
        Assert.Equal(slot.EndTime, result.EndTime);
        Assert.Equal(doctor.FullName, result.DoctorName);
        Assert.Equal(AppointmentStatus.Approved, result.Status);
        Assert.Equal("Follow-up visit", result.Reason);
    }

    #endregion

    #region TC-007: UpdatedAt Timestamp

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-007
    /// Verify: Checkin thành công → UpdatedAt được cập nhật
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_ValidCheckin_UpdatesTimestamp()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        var originalUpdatedAt = appointment.UpdatedAt;

        // Wait a bit to ensure timestamp difference
        await Task.Delay(10);

        // Act
        await _sut.CheckinAppointmentAsync(_appointmentId);

        // Assert
        var updatedAppointment = await _db.Appointments.FindAsync(_appointmentId);
        Assert.True(updatedAppointment!.UpdatedAt >= originalUpdatedAt);
    }

    #endregion

    #region CheckinByCaseIdAsync Tests

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-008
    /// Happy path: Tìm appointment theo caseId → Checkin → Status chuyển thành Approved
    /// </summary>
    [Fact]
    public async Task CheckinByCaseIdAsync_ValidCaseWithBookedAppointment_StatusChangesToApproved()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var caseId = Guid.NewGuid();
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = caseId,
            Status = AppointmentStatus.Booked,
            Reason = "Follow-up",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.CheckinByCaseIdAsync(caseId);

        // Assert
        Assert.Equal(AppointmentStatus.Approved, result.Status);

        // Verify DB was updated
        var updatedAppointment = await _db.Appointments.FindAsync(appointment.AppointmentId);
        Assert.Equal(AppointmentStatus.Approved, updatedAppointment!.Status);
    }

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-009
    /// Edge case: Case không có appointment nào đang BOOKED → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinByCaseIdAsync_NoBookedAppointment_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var caseId = Guid.NewGuid();

        // Tạo appointment đã Approved (không phải Booked)
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = caseId,
            Status = AppointmentStatus.Approved, // Đã checkin rồi
            Reason = "Follow-up",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await SeedAppointmentAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinByCaseIdAsync(caseId));

        Assert.Contains("đã được check-in", ex.Message);
    }

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-010
    /// Edge case: CaseId không tồn tại → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinByCaseIdAsync_CaseNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentCaseId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinByCaseIdAsync(nonExistentCaseId));

        Assert.Contains("Không tìm thấy", ex.Message);
    }

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-011
    /// Verify: CheckinByCaseIdAsync → Response chứa đúng CaseId
    /// </summary>
    [Fact]
    public async Task CheckinByCaseIdAsync_ValidCheckin_ReturnsResponseWithCaseId()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var caseId = Guid.NewGuid();
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = caseId,
            Status = AppointmentStatus.Booked,
            Reason = "Initial visit",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.CheckinByCaseIdAsync(caseId);

        // Assert
        Assert.Equal(caseId, result.CaseId);
        Assert.Equal(AppointmentStatus.Approved, result.Status);
        Assert.Equal(doctor.FullName, result.DoctorName);
    }

    #endregion
}
