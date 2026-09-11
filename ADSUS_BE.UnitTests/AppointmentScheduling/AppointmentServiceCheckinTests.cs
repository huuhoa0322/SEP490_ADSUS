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
/// Flow: Booked (patient đặt lịch) → Completed (nurse checkin khi bệnh nhân đến)
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
        GC.SuppressFinalize(this);
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

    private static PatientProfile CreatePatientProfile(User user)
    {
        return new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            // Gender đã chuyển sang User (2026-01)
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

    private Appointment CreateAppointment(ScheduleSlot slot, PatientProfile profile, AppointmentStatus status, Guid? appointmentId = null)
    {
        return new Appointment
        {
            AppointmentId = appointmentId ?? _appointmentId,
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
    /// Happy path: Appointment đang Booked → Checkin → Status chuyển thành Completed
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_ValidBookedAppointment_StatusChangesToCompleted()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.CheckinAppointmentAsync(_appointmentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AppointmentStatus.Completed, result.Status);

        // Verify DB was updated
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { _appointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Completed, updatedAppointment!.Status);
    }

    #endregion

    #region TC-002: Already Checked-in Appointment (Completed or Approved - backward compat)

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-002
    /// Edge case: Appointment đã checkin (Completed mới hoặc Approved cũ) → Checkin lại → Throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task CheckinAppointmentAsync_AlreadyApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, SlotStatus.Booked);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Completed); // Đã checkin rồi (Completed = mới, Approved = cũ)

        await SeedAppointmentAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinAppointmentAsync(_appointmentId, TestContext.Current.CancellationToken));

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
            () => _sut.CheckinAppointmentAsync(_appointmentId, TestContext.Current.CancellationToken));

        Assert.Contains("ĐÃ ĐẶT", ex.Message);
    }

    #endregion

    #region TC-004: Completed Appointment

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-004
    /// Edge case: Appointment đã Completed (đã check-in trước đó) → Checkin → Throw InvalidOperationException
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
            () => _sut.CheckinAppointmentAsync(_appointmentId, TestContext.Current.CancellationToken));

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
            () => _sut.CheckinAppointmentAsync(nonExistentId, TestContext.Current.CancellationToken));

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
        var result = await _sut.CheckinAppointmentAsync(_appointmentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_appointmentId, result.AppointmentId);
        Assert.Equal(_slotId, result.ScheduleSlotId);
        Assert.Equal(slot.SlotDate, result.SlotDate);
        Assert.Equal(slot.StartTime, result.StartTime);
        Assert.Equal(slot.EndTime, result.EndTime);
        Assert.Equal(doctor.FullName, result.DoctorName);
        Assert.Equal(AppointmentStatus.Completed, result.Status); // Check-in → Completed (thay vì Approved)
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
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Act
        await _sut.CheckinAppointmentAsync(_appointmentId, TestContext.Current.CancellationToken);

        // Assert
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { _appointmentId }, TestContext.Current.CancellationToken);
        Assert.True(updatedAppointment!.UpdatedAt >= originalUpdatedAt);
    }

    #endregion

    #region CheckinByCaseIdAsync Tests

    /// <summary>
    /// TC-UNIT-AppointmentServiceCheckin-008
    /// Happy path: Tìm appointment theo caseId → Checkin → Status chuyển thành Completed
    /// </summary>
    [Fact]
    public async Task CheckinByCaseIdAsync_ValidCaseWithBookedAppointment_StatusChangesToCompleted()
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
        var result = await _sut.CheckinByCaseIdAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AppointmentStatus.Completed, result.Status);

        // Verify DB was updated
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Completed, updatedAppointment!.Status);
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

        // Tạo appointment đã checkin (Completed mới hoặc Approved cũ - backward compat)
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = caseId,
            Status = AppointmentStatus.Completed, // Đã checkin rồi (Completed = mới, Approved = cũ)
            Reason = "Follow-up",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await SeedAppointmentAsync(appointment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckinByCaseIdAsync(caseId, TestContext.Current.CancellationToken));

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
            () => _sut.CheckinByCaseIdAsync(nonExistentCaseId, TestContext.Current.CancellationToken));

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
        var result = await _sut.CheckinByCaseIdAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(caseId, result.CaseId);
        Assert.Equal(AppointmentStatus.Completed, result.Status); // Check-in → Completed (thay vì Approved)
        Assert.Equal(doctor.FullName, result.DoctorName);
    }

    #endregion

    #region GetCheckinQueueAsync Tests

    [Fact]
    public async Task GetCheckinQueueAsync_DateRange_ReturnsAppointmentsWithinRange()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Range");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slot1 = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = today.AddDays(-2),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0),
            Status = SlotStatus.Booked,
        };
        var slot2 = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = today,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Status = SlotStatus.Booked,
        };
        var slot3 = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = today.AddDays(2),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0),
            Status = SlotStatus.Booked,
        };

        var appt1 = CreateAppointment(slot1, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var appt2 = CreateAppointment(slot2, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var appt3 = CreateAppointment(slot3, profile, AppointmentStatus.Booked, Guid.NewGuid());

        await SeedAppointmentAsync(appt1);
        await SeedAppointmentAsync(appt2);
        await SeedAppointmentAsync(appt3);

        // Act - Query range: today to today + 5 days
        var result = await _sut.GetCheckinQueueAsync(
            today, today.AddDays(5), null, "ALL", 1, 15, TestContext.Current.CancellationToken);

        // Assert - slot1 (today - 2) should NOT be included; slot2 and slot3 should be included
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, i => i.AppointmentId == appt2.AppointmentId);
        Assert.Contains(result.Items, i => i.AppointmentId == appt3.AppointmentId);
        Assert.DoesNotContain(result.Items, i => i.AppointmentId == appt1.AppointmentId);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Status");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slot1 = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = today,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0),
            Status = SlotStatus.Booked,
        };
        var slot2 = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = today,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Booked,
        };

        var apptBooked = CreateAppointment(slot1, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var apptCompleted = CreateAppointment(slot2, profile, AppointmentStatus.Completed, Guid.NewGuid());

        await SeedAppointmentAsync(apptBooked);
        await SeedAppointmentAsync(apptCompleted);

        // Act: Filter for COMPLETED
        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, "COMPLETED", 1, 15, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(apptCompleted.AppointmentId, item.AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, item.Status);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCheckinQueueAsync_StatusAllOrEmpty_ExcludesCancelledAppointments_AndReturnsIndependentCounters(string? status)
    {
        // Arrange: Tạo 4 ca với 4 trạng thái khác nhau trong ngày
        var doctor = CreateDoctor("Dr. StatusAll");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slot1 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) };
        var slot2 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) };
        var slot3 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) };
        var slot4 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(12, 0) };

        var apptBooked = CreateAppointment(slot1, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var apptCompleted = CreateAppointment(slot2, profile, AppointmentStatus.Completed, Guid.NewGuid());
        var apptNoShow = CreateAppointment(slot3, profile, AppointmentStatus.NoShow, Guid.NewGuid());
        var apptCancelled = CreateAppointment(slot4, profile, AppointmentStatus.Cancelled, Guid.NewGuid());

        await SeedAppointmentAsync(apptBooked);
        await SeedAppointmentAsync(apptCompleted);
        await SeedAppointmentAsync(apptNoShow);
        await SeedAppointmentAsync(apptCancelled);

        // Act: Filter theo ALL hoặc null/empty
        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, status, 1, 15, TestContext.Current.CancellationToken);

        // Assert: Query phải LOẠI TRỪ Cancelled -> chỉ còn 3 ca (Booked, Completed, NoShow)
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Contains(result.Items, i => i.AppointmentId == apptBooked.AppointmentId && i.Status == AppointmentStatus.Booked);
        Assert.Contains(result.Items, i => i.AppointmentId == apptCompleted.AppointmentId && i.Status == AppointmentStatus.Completed);
        Assert.Contains(result.Items, i => i.AppointmentId == apptNoShow.AppointmentId && i.Status == AppointmentStatus.NoShow);
        Assert.DoesNotContain(result.Items, i => i.AppointmentId == apptCancelled.AppointmentId);
        Assert.DoesNotContain(result.Items, i => i.Status == AppointmentStatus.Cancelled);

        // Assert: Các biến đếm được tính độc lập trên toàn bộ ca trong ngày
        Assert.Equal(1, result.BookedCount);
        Assert.Equal(1, result.CheckedInCount);
        Assert.Equal(1, result.CancelledCount);
        Assert.Equal(1, result.NoShowCount);
    }

    [Theory]
    [InlineData("NOSHOW")]
    [InlineData("noshow")]
    [InlineData("NO_SHOW")]
    [InlineData("no_show")]
    public async Task GetCheckinQueueAsync_StatusNoShow_ReturnsOnlyNoShowAppointments_AndCountersRemainIndependent(string status)
    {
        // Arrange: Tạo 4 ca đủ 4 trạng thái
        var doctor = CreateDoctor("Dr. NoShowTest");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slot1 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) };
        var slot2 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) };
        var slot3 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) };
        var slot4 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(12, 0) };

        var apptBooked = CreateAppointment(slot1, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var apptCompleted = CreateAppointment(slot2, profile, AppointmentStatus.Completed, Guid.NewGuid());
        var apptNoShow = CreateAppointment(slot3, profile, AppointmentStatus.NoShow, Guid.NewGuid());
        var apptCancelled = CreateAppointment(slot4, profile, AppointmentStatus.Cancelled, Guid.NewGuid());

        await SeedAppointmentAsync(apptBooked);
        await SeedAppointmentAsync(apptCompleted);
        await SeedAppointmentAsync(apptNoShow);
        await SeedAppointmentAsync(apptCancelled);

        // Act: Filter chỉ lấy NOSHOW
        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, status, 1, 15, TestContext.Current.CancellationToken);

        // Assert: Chỉ có duy nhất ca NoShow
        Assert.Equal(1, result.TotalCount);
        var singleItem = Assert.Single(result.Items);
        Assert.Equal(apptNoShow.AppointmentId, singleItem.AppointmentId);
        Assert.Equal(AppointmentStatus.NoShow, singleItem.Status);

        // Assert: Counters vẫn phản ánh toàn bộ ca trong ngày (không bị ảnh hưởng bởi status filter)
        Assert.Equal(1, result.BookedCount);
        Assert.Equal(1, result.CheckedInCount);
        Assert.Equal(1, result.CancelledCount);
        Assert.Equal(1, result.NoShowCount);
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("cancelled")]
    [InlineData("Cancelled")]
    public async Task GetCheckinQueueAsync_StatusCancelled_ReturnsOnlyCancelledAppointments_AndCountersRemainIndependent(string status)
    {
        // Arrange: Tạo 4 ca đủ 4 trạng thái
        var doctor = CreateDoctor("Dr. CancelledTest");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slot1 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) };
        var slot2 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) };
        var slot3 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) };
        var slot4 = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(12, 0) };

        var apptBooked = CreateAppointment(slot1, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var apptCompleted = CreateAppointment(slot2, profile, AppointmentStatus.Completed, Guid.NewGuid());
        var apptNoShow = CreateAppointment(slot3, profile, AppointmentStatus.NoShow, Guid.NewGuid());
        var apptCancelled = CreateAppointment(slot4, profile, AppointmentStatus.Cancelled, Guid.NewGuid());

        await SeedAppointmentAsync(apptBooked);
        await SeedAppointmentAsync(apptCompleted);
        await SeedAppointmentAsync(apptNoShow);
        await SeedAppointmentAsync(apptCancelled);

        // Act: Filter chỉ lấy CANCELLED
        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, status, 1, 15, TestContext.Current.CancellationToken);

        // Assert: Chỉ có duy nhất ca Cancelled
        Assert.Equal(1, result.TotalCount);
        var singleItem = Assert.Single(result.Items);
        Assert.Equal(apptCancelled.AppointmentId, singleItem.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, singleItem.Status);

        // Assert: Counters vẫn phản ánh toàn bộ ca trong ngày
        Assert.Equal(1, result.BookedCount);
        Assert.Equal(1, result.CheckedInCount);
        Assert.Equal(1, result.CancelledCount);
        Assert.Equal(1, result.NoShowCount);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_SortingOrder_PrioritizesBookedThenCompletedThenNoShowThenSlotTime()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Sorter");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Tạo slot với thời gian không theo thứ tự status
        var slotNoShow = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) };
        var slotCompleted = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) };
        var slotBookedLate = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(12, 0) };
        var slotBookedEarly = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) };

        var apptNoShow = CreateAppointment(slotNoShow, profile, AppointmentStatus.NoShow, Guid.NewGuid());
        var apptCompleted = CreateAppointment(slotCompleted, profile, AppointmentStatus.Completed, Guid.NewGuid());
        var apptBookedLate = CreateAppointment(slotBookedLate, profile, AppointmentStatus.Booked, Guid.NewGuid());
        var apptBookedEarly = CreateAppointment(slotBookedEarly, profile, AppointmentStatus.Booked, Guid.NewGuid());

        await SeedAppointmentAsync(apptNoShow);
        await SeedAppointmentAsync(apptCompleted);
        await SeedAppointmentAsync(apptBookedLate);
        await SeedAppointmentAsync(apptBookedEarly);

        // Act: Filter ALL
        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, "ALL", 1, 15, TestContext.Current.CancellationToken);

        // Assert: Sắp xếp theo: Booked (theo StartTime) -> Completed -> NoShow
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(apptBookedEarly.AppointmentId, result.Items[0].AppointmentId);
        Assert.Equal(AppointmentStatus.Booked, result.Items[0].Status);

        Assert.Equal(apptBookedLate.AppointmentId, result.Items[1].AppointmentId);
        Assert.Equal(AppointmentStatus.Booked, result.Items[1].Status);

        Assert.Equal(apptCompleted.AppointmentId, result.Items[2].AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, result.Items[2].Status);

        Assert.Equal(apptNoShow.AppointmentId, result.Items[3].AppointmentId);
        Assert.Equal(AppointmentStatus.NoShow, result.Items[3].Status);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_Search_FiltersByPatientNameDoctorPhoneReason()
    {
        // Arrange
        var doctorA = new User { UserId = Guid.NewGuid(), FullName = "Dr. Strange", Phone = "0111", PasswordHash = "x", Role = UserRole.Doctor };
        var doctorB = new User { UserId = Guid.NewGuid(), FullName = "Dr. House", Phone = "0222", PasswordHash = "x", Role = UserRole.Doctor };

        var patientA = new User { UserId = Guid.NewGuid(), FullName = "Tony Stark", Phone = "0987654321", PasswordHash = "x", Role = UserRole.Patient };
        var patientB = new User { UserId = Guid.NewGuid(), FullName = "Bruce Wayne", Phone = "0123456789", PasswordHash = "x", Role = UserRole.Patient };

        var profileA = CreatePatientProfile(patientA);
        var profileB = CreatePatientProfile(patientB);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slotA = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctorA.UserId, Doctor = doctorA, SlotDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) };
        var slotB = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctorB.UserId, Doctor = doctorB, SlotDate = today, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0) };

        var apptA = CreateAppointment(slotA, profileA, AppointmentStatus.Booked, Guid.NewGuid());
        apptA.Reason = "Cardiac evaluation";

        var apptB = CreateAppointment(slotB, profileB, AppointmentStatus.Booked, Guid.NewGuid());
        apptB.Reason = "General wellness";

        await SeedAppointmentAsync(apptA);
        await SeedAppointmentAsync(apptB);

        // Act 1: search patient name "Stark"
        var res1 = await _sut.GetCheckinQueueAsync(today, today, "Stark", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Single(res1.Items);
        Assert.Equal("Tony Stark", res1.Items[0].PatientFullName);

        // Act 2: search doctor name "House"
        var res2 = await _sut.GetCheckinQueueAsync(today, today, "House", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Single(res2.Items);
        Assert.Equal("Bruce Wayne", res2.Items[0].PatientFullName);

        // Act 3: search reason "Cardiac"
        var res3 = await _sut.GetCheckinQueueAsync(today, today, "Cardiac", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Single(res3.Items);
        Assert.Equal(apptA.AppointmentId, res3.Items[0].AppointmentId);

        // Act 4: search phone "012345"
        var res4 = await _sut.GetCheckinQueueAsync(today, today, "012345", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Single(res4.Items);
        Assert.Equal("Bruce Wayne", res4.Items[0].PatientFullName);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_Pagination_CalculatesPagesAndSlicesItems()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Pager");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (int i = 0; i < 25; i++)
        {
            var slot = new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = doctor.UserId,
                Doctor = doctor,
                SlotDate = today,
                StartTime = new TimeOnly(8 + (i / 60), i % 60),
                EndTime = new TimeOnly(9 + (i / 60), i % 60),
            };
            var appt = CreateAppointment(slot, profile, AppointmentStatus.Booked, Guid.NewGuid());
            await SeedAppointmentAsync(appt);
        }

        // Act: Page 1, PageSize 10
        var page1 = await _sut.GetCheckinQueueAsync(today, today, null, null, 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(10, page1.PageSize);
        Assert.Equal(3, page1.TotalPages);

        // Act: Page 3, PageSize 10 (should have 5 items left)
        var page3 = await _sut.GetCheckinQueueAsync(today, today, null, null, 3, 10, TestContext.Current.CancellationToken);
        Assert.Equal(5, page3.Items.Count);
        Assert.Equal(3, page3.Page);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_SingleDate_BackwardCompatible()
    {
        // Arrange
        var doctor = CreateDoctor("Dr. Legacy");
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var slot = new ScheduleSlot { SlotId = Guid.NewGuid(), DoctorId = doctor.UserId, Doctor = doctor, SlotDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0) };
        var appt = CreateAppointment(slot, profile, AppointmentStatus.Booked, Guid.NewGuid());
        await SeedAppointmentAsync(appt);

        // Act: call single date method
        var result = await _sut.GetCheckinQueueAsync(today, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, i => i.AppointmentId == appt.AppointmentId);
    }

    #endregion
}
