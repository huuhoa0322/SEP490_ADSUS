using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
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
/// Unit tests cho NoShowService (Module 8 - Nurse Checkin No-Show).
/// Business rule: Appointment đang BOOKED quá grace time (15 phút) sau giờ bắt đầu khám → Auto-NoShow
/// </summary>
public class NoShowServiceTests : IDisposable
{
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<ILogger<NoShowService>> _logger = new();
    private readonly AppDbContext _db;
    private readonly NoShowService _sut;

    public NoShowServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var noShowSettings = Options.Create(new NoShowSettings { GraceTimeMinutes = 15 });
        _sut = new NoShowService(
            _db,
            noShowSettings,
            _notificationService.Object,
            _profileRepo.Object,
            _logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private static User CreateDoctor()
    {
        var userId = Guid.NewGuid();
        return new User
        {
            UserId = userId,
            Phone = "0900000001",
            FullName = "Dr. Test",
            Email = "doctor@test.com",
            PasswordHash = "hash",
            Status = UserStatus.Active,
            Role = UserRole.Doctor,
        };
    }

    private static User CreatePatient()
    {
        var userId = Guid.NewGuid();
        return new User
        {
            UserId = userId,
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
            Gender = GenderType.Female,
            CreatedBy = Guid.NewGuid(),
        };
    }

    private static ScheduleSlot CreateSlot(User doctor, DateOnly date, TimeOnly startTime)
    {
        return new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = date,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(30),
            Status = SlotStatus.Booked,
        };
    }

    private static Appointment CreateAppointment(ScheduleSlot slot, PatientProfile profile, AppointmentStatus status)
    {
        return new Appointment
        {
            AppointmentId = Guid.NewGuid(),
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

    #region TC-001: Status Not Booked - Should Not Process

    /// <summary>
    /// TC-UNIT-NoShowService-001
    /// Appointment đang Approved (đã checkin) → Không xử lý
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_StatusNotBooked_ReturnsFalse()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-2)), TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-2)));
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Approved);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.WasProcessed);
        Assert.Null(result.PreviousStatus);
    }

    #endregion

    #region TC-002: Within Grace Time - Should Not Process

    /// <summary>
    /// TC-UNIT-NoShowService-002
    /// Appointment trong grace time (5 phút) → Không xử lý
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_WithinGraceTime_ReturnsRemainingMinutes()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        // Use a future appointment - this definitely won't trigger no-show processing
        // slotDate = tomorrow, startTime = 08:00 AM Vietnam = 01:00 UTC next day
        var slotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var startTime = new TimeOnly(1, 0, 0); // 01:00 UTC

        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert - Future appointment, no processing
        Assert.False(result.WasProcessed);
        Assert.True(result.RemainingMinutes > 0);
    }

    #endregion

    #region TC-003: Past Grace Time - Marks as NoShow

    /// <summary>
    /// TC-UNIT-NoShowService-003
    /// Appointment quá grace time (2 tiếng) → Đánh dấu NoShow
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_PastGraceTime_MarksAsNoShow()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.WasProcessed);
        Assert.Equal(AppointmentStatus.Booked, result.PreviousStatus);

        // Verify DB was updated
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, updatedAppointment!.Status);
        Assert.NotNull(updatedAppointment.CancelledReason);
        Assert.Contains("15 phút", updatedAppointment.CancelledReason);
    }

    #endregion

    #region TC-004: Sends Patient Notification

    /// <summary>
    /// TC-UNIT-NoShowService-004
    /// Khi đánh dấu NoShow → Gửi notification cho patient
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_SendsPatientNotification()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        _profileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == patient.UserId &&
                r.Type == "appointment_no_show" &&
                r.Title.Contains("hủy")
            ), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TC-005: Sends Doctor Notification

    /// <summary>
    /// TC-UNIT-NoShowService-005
    /// Khi đánh dấu NoShow → Gửi notification cho doctor
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_SendsDoctorNotification()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        _profileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == doctor.UserId &&
                r.Type == "patient_no_show"
            ), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TC-006: Patient Notification Failure Does Not Throw

    /// <summary>
    /// TC-UNIT-NoShowService-006
    /// Nếu gửi patient notification thất bại → Không throw, vẫn mark NoShow
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_PatientNotificationFails_DoesNotThrow()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        _profileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Setup notification để throw exception
        _notificationService
            .Setup(s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Notification failed"));

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act - Không throw
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.WasProcessed);
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, updatedAppointment!.Status);
    }

    #endregion

    #region TC-007: Doctor Notification Failure Does Not Throw

    /// <summary>
    /// TC-UNIT-NoShowService-007
    /// Nếu gửi doctor notification thất bại → Không throw, vẫn mark NoShow
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_DoctorNotificationFails_DoesNotThrow()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        _profileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var callCount = 0;
        _notificationService
            .Setup(s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                // Chỉ appointment đầu tiên (patient notification) throw
                if (callCount == 1) throw new Exception("Patient notification failed");
                return Task.FromResult(Guid.NewGuid());
            });

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act - Không throw
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.WasProcessed);
    }

    #endregion

    #region TC-008: Profile Not Found - Does Not Throw

    /// <summary>
    /// TC-UNIT-NoShowService-008
    /// Patient profile không tìm thấy → Vẫn mark NoShow, không throw
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_ProfileNotFound_DoesNotThrow()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        _profileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.WasProcessed);
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, updatedAppointment!.Status);
    }

    #endregion

    #region TC-009: Cancelled Appointment - Should Not Process

    /// <summary>
    /// TC-UNIT-NoShowService-009
    /// Appointment đã Cancelled → Không xử lý
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_AlreadyCancelled_ReturnsFalse()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Cancelled);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.WasProcessed);
    }

    #endregion

    #region TC-010: NoShow Appointment - Should Not Process Again

    /// <summary>
    /// TC-UNIT-NoShowService-010
    /// Appointment đã NoShow → Không xử lý lại
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_AlreadyNoShow_ReturnsFalse()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.NoShow);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.WasProcessed);
    }

    #endregion

    #region TC-011: UpdatedAt Timestamp Updated

    /// <summary>
    /// TC-UNIT-NoShowService-011
    /// Khi mark NoShow → UpdatedAt được cập nhật
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_UpdatesTimestamp()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        var originalUpdatedAt = appointment.UpdatedAt;
        await Task.Delay(10, TestContext.Current.CancellationToken); // Ensure timestamp difference

        // Act
        await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.True(updatedAppointment!.UpdatedAt >= originalUpdatedAt);
    }

    #endregion

    #region TC-012: Exactly At Grace Time Boundary

    /// <summary>
    /// TC-UNIT-NoShowService-012
    /// Appointment đúng tại grace time boundary (15 phút) → Xử lý
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_ExactlyAtGraceTime_MarksAsNoShow()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var now = DateTime.UtcNow;
        // Slot started exactly GraceTimeMinutes ago
        var slotDate = DateOnly.FromDateTime(now.AddMinutes(-15));
        var startTime = TimeOnly.FromDateTime(now.AddMinutes(-15));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert - At exactly grace time should be processed
        Assert.True(result.WasProcessed);
    }

    #endregion
}
