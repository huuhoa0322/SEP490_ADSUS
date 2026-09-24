using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace ADSUS_BE.UnitTests.Jobs;

/// <summary>
/// Unit tests cho NoShowCancellationJob (JOB-08).
/// Business rule: Appointment đang BOOKED quá grace time (15 phút) sau giờ bắt đầu khám → Auto-NoShow
/// </summary>
public class NoShowCancellationJobTests : IDisposable
{
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ILogger<NoShowCancellationJob>> _logger = new();
    private readonly AppDbContext _db;
    private readonly NoShowCancellationJob _sut;

    public NoShowCancellationJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        // Tạo mock IServiceScopeFactory — job lấy NoShowService (thật, chạy trên DB InMemory) từ scope
        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(NoShowService)))
            .Returns(NoShowTestServices.Create(_db, _notificationService.Object));
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        _sut = new NoShowCancellationJob(
            scopeFactory.Object,
            _logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private static User CreateDoctor(string name = "Dr. Test")
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = name,
            Phone = "0900000001",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
        };
    }

    private static User CreatePatient(string name = "Patient Test")
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = name,
            Phone = "0900000002",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
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
        };
    }

    private async Task<Appointment> SeedAppointmentAsync(Appointment appointment)
    {
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return appointment;
    }

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var context = new Mock<IJobExecutionContext>();
        return context.Object;
    }

    #endregion

    #region TC-001: No Appointments to Process

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-001: Không có appointment nào → Không làm gì
    /// </summary>
    [Fact]
    public async Task Execute_NoAppointments_DoesNothing()
    {
        // Arrange
        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Không có appointment nào bị cancel
        var appointments = await _db.Appointments.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(appointments);
    }

    #endregion

    #region TC-002: Appointment Within Threshold - Should NOT Cancel

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-002: Appointment trong ngưỡng thời gian (chưa quá 15 phút)
    /// → Không bị cancel
    /// </summary>
    [Fact]
    public async Task Execute_AppointmentWithinThreshold_DoesNotCancel()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        // Slot bắt đầu 10 phút trước theo giờ phòng khám (vẫn trong grace time 15 phút)
        var clinicNow = DateTime.UtcNow.Add(ClinicClock.Offset);
        var slotDate = DateOnly.FromDateTime(clinicNow.AddMinutes(-10));
        var startTime = TimeOnly.FromDateTime(clinicNow.AddMinutes(-10));

        var slot = CreateSlot(doctor, slotDate, startTime);
        slot.Status = SlotStatus.Booked;
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Appointment vẫn Booked (chưa quá grace time)
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Booked, updatedAppointment!.Status);
    }

    #endregion

    #region TC-003: Appointment Past Threshold - Should Cancel

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-003: Appointment đã quá ngưỡng grace time (15 phút)
    /// → Bị cancel
    /// </summary>
    [Fact]
    public async Task Execute_AppointmentPastThreshold_StatusChangesToCancelled()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        // Slot bắt đầu 2 tiếng trước (đã quá 1 tiếng)
        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));

        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Appointment đã bị cancel
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, updatedAppointment!.Status);
        Assert.NotNull(updatedAppointment.CancelledReason);
    }

    #endregion

    #region TC-004: Already Completed - Should NOT Cancel

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-004: Appointment đã Completed (đã checkin)
    /// → Không bị cancel dù đã quá thời gian
    /// </summary>
    [Fact]
    public async Task Execute_AlreadyCompleted_DoesNotCancel()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        // Slot bắt đầu 2 tiếng trước
        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));

        var slot = CreateSlot(doctor, slotDate, startTime);
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Completed);

        await SeedAppointmentAsync(appointment);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Appointment vẫn Completed
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Completed, updatedAppointment!.Status);
    }

    #endregion

    #region TC-005: Already Cancelled - Should NOT Cancel Again

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-005: Appointment đã Cancelled trước đó
    /// → Không làm gì
    /// </summary>
    [Fact]
    public async Task Execute_AlreadyCancelled_DoesNothing()
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

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Appointment vẫn Cancelled (không thay đổi)
        var updatedAppointment = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Cancelled, updatedAppointment!.Status);
    }

    #endregion

    #region TC-006: Multiple Appointments - Mixed Scenarios

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-006: Nhiều appointment với các trạng thái khác nhau
    /// → Chỉ cancel appointment quá ngưỡng và đang Booked
    /// </summary>
    [Fact]
    public async Task Execute_MultipleAppointments_OnlyCancelsNoShow()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var clinicNow = DateTime.UtcNow.Add(ClinicClock.Offset);

        // Appointment 1: Quá ngưỡng, đang Booked → Sẽ bị cancel
        var slot1Date = DateOnly.FromDateTime(clinicNow.AddHours(-2));
        var slot1Time = TimeOnly.FromDateTime(clinicNow.AddHours(-2));
        var slot1 = CreateSlot(doctor, slot1Date, slot1Time);
        var appointment1 = CreateAppointment(slot1, profile, AppointmentStatus.Booked);

        // Appointment 2: Trong ngưỡng, đang Booked → Không cancel
        var slot2Date = DateOnly.FromDateTime(clinicNow.AddMinutes(-10));
        var slot2Time = TimeOnly.FromDateTime(clinicNow.AddMinutes(-10));
        var slot2 = CreateSlot(doctor, slot2Date, slot2Time);
        var appointment2 = CreateAppointment(slot2, profile, AppointmentStatus.Booked);

        // Appointment 3: Quá ngưỡng, đã Completed → Không cancel
        var slot3Date = DateOnly.FromDateTime(clinicNow.AddHours(-2));
        var slot3Time = TimeOnly.FromDateTime(clinicNow.AddHours(-2));
        var slot3 = CreateSlot(doctor, slot3Date, slot3Time);
        var appointment3 = CreateAppointment(slot3, profile, AppointmentStatus.Completed);

        _db.Appointments.AddRange(appointment1, appointment2, appointment3);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        var ap1 = await _db.Appointments.FindAsync(new object[] { appointment1.AppointmentId }, TestContext.Current.CancellationToken);
        var ap2 = await _db.Appointments.FindAsync(new object[] { appointment2.AppointmentId }, TestContext.Current.CancellationToken);
        var ap3 = await _db.Appointments.FindAsync(new object[] { appointment3.AppointmentId }, TestContext.Current.CancellationToken);

        Assert.Equal(AppointmentStatus.NoShow, ap1!.Status); // Đã bị no-show
        Assert.Equal(AppointmentStatus.Booked, ap2!.Status);   // Vẫn Booked
        Assert.Equal(AppointmentStatus.Completed, ap3!.Status); // Vẫn Completed
    }

    #endregion

    #region TC-007: Slot Status Changes to Open

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-007: Khi appointment bị cancel, slot chuyển sang OPEN
    /// </summary>
    [Fact]
    public async Task Execute_AppointmentCancelled_SlotStatusChangesToOpen()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var now = DateTime.UtcNow;
        var slotDate = DateOnly.FromDateTime(now.AddHours(-2));
        var startTime = TimeOnly.FromDateTime(now.AddHours(-2));

        var slot = CreateSlot(doctor, slotDate, startTime);
        slot.Status = SlotStatus.Booked;
        var appointment = CreateAppointment(slot, profile, AppointmentStatus.Booked);

        await SeedAppointmentAsync(appointment);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Slot đã chuyển sang Open
        var updatedSlot = await _db.ScheduleSlots.FindAsync(new object[] { slot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Open, updatedSlot!.Status);
    }

    #endregion

    #region TC-008: Notification Sent on Cancellation

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-008: Khi appointment bị cancel, gửi notification cho bệnh nhân
    /// </summary>
    [Fact]
    public async Task Execute_AppointmentCancelled_SendsNotification()
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

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Notification đã được gửi (cùng loại với luồng check-in muộn — NoShowService)
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == patient.UserId &&
                r.Type == "appointment_no_show" &&
                r.Title == "Lịch khám đã bị hủy (No-Show)"),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == doctor.UserId &&
                r.Type == "patient_no_show"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TC-009: Notification Failure Does Not Stop Processing

    /// <summary>
    /// TC-UNIT-NoShowCancellationJob-009: Nếu gửi notification thất bại, vẫn tiếp tục xử lý các appointment khác
    /// </summary>
    [Fact]
    public async Task Execute_NotificationFails_ContinuesProcessing()
    {
        // Arrange
        var doctor = CreateDoctor();
        var patient1 = CreatePatient("Patient 1");
        var patient2 = CreatePatient("Patient 2");
        var profile1 = CreatePatientProfile(patient1);
        var profile2 = CreatePatientProfile(patient2);

        var now = DateTime.UtcNow;

        // Appointment 1: Quá ngưỡng - notification sẽ throw
        var slot1Date = DateOnly.FromDateTime(now.AddHours(-2));
        var slot1Time = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot1 = CreateSlot(doctor, slot1Date, slot1Time);
        var appointment1 = CreateAppointment(slot1, profile1, AppointmentStatus.Booked);

        // Appointment 2: Quá ngưỡng - notification sẽ throw
        var slot2Date = DateOnly.FromDateTime(now.AddHours(-2));
        var slot2Time = TimeOnly.FromDateTime(now.AddHours(-2));
        var slot2 = CreateSlot(doctor, slot2Date, slot2Time);
        var appointment2 = CreateAppointment(slot2, profile2, AppointmentStatus.Booked);

        _db.Appointments.AddRange(appointment1, appointment2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Setup notification để throw exception cho appointment đầu tiên
        var callCount = 0;
        _notificationService
            .Setup(s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("Notification failed");
                return Task.FromResult(Guid.NewGuid());
            });

        var context = CreateMockJobExecutionContext();

        // Act - Không throw exception
        await _sut.Execute(context);

        // Assert - Cả 2 appointment đều bị no-show dù notification fail
        var ap1 = await _db.Appointments.FindAsync(new object[] { appointment1.AppointmentId }, TestContext.Current.CancellationToken);
        var ap2 = await _db.Appointments.FindAsync(new object[] { appointment2.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.NoShow, ap1!.Status);
        Assert.Equal(AppointmentStatus.NoShow, ap2!.Status);
    }

    #endregion

    #region TC-010: Linked Case Loaded With The Appointment

    /// <summary>
    /// Case liên kết được nạp cùng lịch hẹn (Include) thay vì truy vấn riêng từng lịch: Case còn
    /// Booked thì chuyển Cancelled, Case đã sang trạng thái khác thì giữ nguyên.
    /// </summary>
    [Fact]
    public async Task Execute_NoShowWithLinkedCase_CancelsOnlyBookedCase()
    {
        // Arrange — 2 lịch quá hạn (theo giờ phòng khám), mỗi lịch gắn 1 Case
        var doctor = CreateDoctor();
        var clinicPast = DateTime.UtcNow.Add(ClinicClock.Offset).AddHours(-2);
        var date = DateOnly.FromDateTime(clinicPast);

        var bookedCase = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor.UserId, Status = CaseStatus.Booked };
        var inProgressCase = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor.UserId, Status = CaseStatus.InProgress };

        var profile1 = CreatePatientProfile(CreatePatient("P1"));
        var ap1 = CreateAppointment(CreateSlot(doctor, date, TimeOnly.FromDateTime(clinicPast)), profile1, AppointmentStatus.Booked);
        bookedCase.PatientProfileId = profile1.PatientProfileId;
        ap1.CaseId = bookedCase.CaseId;

        var profile2 = CreatePatientProfile(CreatePatient("P2"));
        var clinicPast2 = clinicPast.AddMinutes(-30);
        var ap2 = CreateAppointment(CreateSlot(doctor, DateOnly.FromDateTime(clinicPast2), TimeOnly.FromDateTime(clinicPast2)), profile2, AppointmentStatus.Booked);
        inProgressCase.PatientProfileId = profile2.PatientProfileId;
        ap2.CaseId = inProgressCase.CaseId;

        _db.Cases.AddRange(bookedCase, inProgressCase);
        _db.Appointments.AddRange(ap1, ap2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _sut.Execute(CreateMockJobExecutionContext());

        // Assert
        Assert.Equal(CaseStatus.Cancelled, (await _db.Cases.FindAsync(new object[] { bookedCase.CaseId }, TestContext.Current.CancellationToken))!.Status);
        Assert.Equal(CaseStatus.InProgress, (await _db.Cases.FindAsync(new object[] { inProgressCase.CaseId }, TestContext.Current.CancellationToken))!.Status);
    }

    /// <summary>Lịch ngày mai theo giờ phòng khám không bị đụng tới (lọc thời gian nằm trong SQL).</summary>
    [Fact]
    public async Task Execute_FutureAppointment_IsLeftBooked()
    {
        var doctor = CreateDoctor();
        var tomorrow = ClinicClock.Today().AddDays(1);
        var appointment = CreateAppointment(
            CreateSlot(doctor, tomorrow, new TimeOnly(8, 0)),
            CreatePatientProfile(CreatePatient()),
            AppointmentStatus.Booked);
        await SeedAppointmentAsync(appointment);

        await _sut.Execute(CreateMockJobExecutionContext());

        var updated = await _db.Appointments.FindAsync(new object[] { appointment.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(AppointmentStatus.Booked, updated!.Status);
    }

    #endregion
}
