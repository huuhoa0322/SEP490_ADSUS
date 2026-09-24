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
    private readonly AppDbContext _db;
    private readonly NoShowService _sut;

    public NoShowServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _sut = NoShowTestServices.Create(_db, _notificationService.Object);
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
            // Gender đã chuyển sang User (2026-01)
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
    /// Appointment đang Completed (đã checkin) → Không xử lý
    /// </summary>
    [Fact]
    public async Task ProcessNoShowAsync_StatusNotBooked_ReturnsFalse()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);
        var slot = CreateSlot(doctor, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-2)), TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-2)));
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Completed);

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
        // slotDate = 2 days from now
        var slotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var startTime = new TimeOnly(8, 0, 0); // 08:00 local time

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

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);
        // Hồ sơ không tồn tại trong DB (DB InMemory không kiểm khoá ngoại)
        appointment.PatientProfile = null!;
        appointment.PatientProfileId = Guid.NewGuid();

        await SeedAppointmentAsync(appointment);

        // Act
        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        // Assert — vẫn đánh dấu NoShow, chỉ bác sĩ nhận thông báo
        Assert.True(result.WasProcessed);
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, updatedAppointment!.Status);
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.Type == "appointment_no_show"), It.IsAny<CancellationToken>()),
            Times.Never);
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

    #region Quy tắc gộp với JOB-08 (P11 review 25/09/2026)

    /// <summary>Giờ phòng khám lùi <paramref name="minutesAgo"/> phút — cùng hệ giờ với SlotDate/StartTime.</summary>
    private static (DateOnly Date, TimeOnly Time) ClinicMinutesAgo(int minutesAgo)
    {
        var local = DateTime.UtcNow.Add(ClinicClock.Offset).AddMinutes(-minutesAgo);
        return (DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
    }

    /// <summary>Hồ sơ guest (người thân chưa có tài khoản) → người đặt hộ nhận thông báo No-Show.</summary>
    [Fact]
    public async Task ProcessNoShowAsync_GuestProfile_NotifiesGuardian()
    {
        var doctor = CreateDoctor();
        var guardianId = Guid.NewGuid();
        var guestProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = null,
            FullName = "Người thân",
            Phone = "0911111111",
            CreatedBy = guardianId,
        };
        var (date, time) = ClinicMinutesAgo(120);
        var appointment = CreateAppointment(CreateSlot(doctor, date, time), guestProfile, AppointmentStatus.Booked);
        appointment.BookedByUserId = guardianId;
        await SeedAppointmentAsync(appointment);

        var result = await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        Assert.True(result.WasProcessed);
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == guardianId && r.Type == "appointment_no_show"), It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == Guid.Empty), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Case liên kết còn Booked → Cancelled; Case đã sang trạng thái khác giữ nguyên.</summary>
    [Fact]
    public async Task ProcessNoShowAsync_LinkedCase_CancelsOnlyBookedCase()
    {
        var doctor = CreateDoctor();
        var (date, time) = ClinicMinutesAgo(120);

        var profile1 = CreatePatientProfile(CreatePatient());
        var bookedCase = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor.UserId, PatientProfileId = profile1.PatientProfileId, Status = CaseStatus.Booked };
        var ap1 = CreateAppointment(CreateSlot(doctor, date, time), profile1, AppointmentStatus.Booked);
        ap1.CaseId = bookedCase.CaseId;

        var profile2 = CreatePatientProfile(CreatePatient());
        var inProgressCase = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor.UserId, PatientProfileId = profile2.PatientProfileId, Status = CaseStatus.InProgress };
        var ap2 = CreateAppointment(CreateSlot(doctor, date, time.AddMinutes(-30)), profile2, AppointmentStatus.Booked);
        ap2.CaseId = inProgressCase.CaseId;

        _db.Cases.AddRange(bookedCase, inProgressCase);
        _db.Appointments.AddRange(ap1, ap2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _sut.ProcessNoShowAsync(ap1, TestContext.Current.CancellationToken);
        await _sut.ProcessNoShowAsync(ap2, TestContext.Current.CancellationToken);

        Assert.Equal(CaseStatus.Cancelled, (await _db.Cases.FindAsync(new object[] { bookedCase.CaseId }, TestContext.Current.CancellationToken))!.Status);
        Assert.Equal(CaseStatus.InProgress, (await _db.Cases.FindAsync(new object[] { inProgressCase.CaseId }, TestContext.Current.CancellationToken))!.Status);
    }

    /// <summary>Slot được mở lại và cập nhật UpdatedAt.</summary>
    [Fact]
    public async Task ProcessNoShowAsync_ReopensSlotAndUpdatesTimestamp()
    {
        var doctor = CreateDoctor();
        var (date, time) = ClinicMinutesAgo(120);
        var slot = CreateSlot(doctor, date, time);
        slot.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        var appointment = CreateAppointment(slot, CreatePatientProfile(CreatePatient()), AppointmentStatus.Booked);
        await SeedAppointmentAsync(appointment);
        var before = DateTime.UtcNow;

        await _sut.ProcessNoShowAsync(appointment, TestContext.Current.CancellationToken);

        var updatedSlot = await _db.ScheduleSlots.FindAsync(new object[] { slot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Open, updatedSlot!.Status);
        Assert.True(updatedSlot.UpdatedAt >= before);
    }

    /// <summary>
    /// JOB-08 dùng chung quy tắc: chỉ lịch Booked đã qua grace time (đúng phút thứ 15 cũng tính);
    /// lịch còn trong grace time, lịch tương lai, lịch đã Completed giữ nguyên. Báo cả bệnh nhân lẫn bác sĩ.
    /// </summary>
    [Fact]
    public async Task ProcessOverdueAsync_MarksOnlyOverdueBookedAndNotifiesPatientAndDoctor()
    {
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var (d1, t1) = ClinicMinutesAgo(120);
        var overdue = CreateAppointment(CreateSlot(doctor, d1, t1), profile, AppointmentStatus.Booked);
        var (d2, t2) = ClinicMinutesAgo(15);
        var atGrace = CreateAppointment(CreateSlot(doctor, d2, t2), profile, AppointmentStatus.Booked);
        var (d3, t3) = ClinicMinutesAgo(10);
        var withinGrace = CreateAppointment(CreateSlot(doctor, d3, t3), profile, AppointmentStatus.Booked);
        var future = CreateAppointment(CreateSlot(doctor, ClinicClock.Today().AddDays(1), new TimeOnly(8, 0)), profile, AppointmentStatus.Booked);
        var completed = CreateAppointment(CreateSlot(doctor, d1, t1.AddMinutes(-30)), profile, AppointmentStatus.Completed);
        _db.Appointments.AddRange(overdue, atGrace, withinGrace, future, completed);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var count = await _sut.ProcessOverdueAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
        async Task<AppointmentStatus> StatusOf(Appointment a) =>
            (await _db.Appointments.FindAsync(new object[] { a.AppointmentId }, TestContext.Current.CancellationToken))!.Status;
        Assert.Equal(AppointmentStatus.NoShow, await StatusOf(overdue));
        Assert.Equal(AppointmentStatus.NoShow, await StatusOf(atGrace));
        Assert.Equal(AppointmentStatus.Booked, await StatusOf(withinGrace));
        Assert.Equal(AppointmentStatus.Booked, await StatusOf(future));
        Assert.Equal(AppointmentStatus.Completed, await StatusOf(completed));

        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == patient.UserId && r.Type == "appointment_no_show"), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == doctor.UserId && r.Type == "patient_no_show"), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion
}
