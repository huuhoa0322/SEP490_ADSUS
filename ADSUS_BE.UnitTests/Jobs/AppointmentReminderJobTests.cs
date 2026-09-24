using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ADSUS_BE.UnitTests.Jobs;

/// <summary>
/// Unit tests cho AppointmentReminderJob (JOB-03).
/// Gửi nhắc nhở lịch hẹn trước 24 giờ. Cửa sổ 20-24 giờ được lọc trong repository nên test chạy
/// trên AppointmentRepository thật (DB InMemory). Giờ slot trong DB là giờ địa phương phòng khám.
/// </summary>
public class AppointmentReminderJobTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ILogger<AppointmentReminderJob>> _logger = new();
    private readonly AppointmentReminderJob _sut;
    private readonly User _doctor;

    public AppointmentReminderJobTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(INotificationService))).Returns(_notificationService.Object);
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        _sut = new AppointmentReminderJob(
            scopeFactory.Object,
            new AppointmentRepository(_db),
            _logger.Object);

        _doctor = CreateUser("Dr. Test", UserRole.Doctor);
        _db.Users.Add(_doctor);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var context = new Mock<IJobExecutionContext>();
        return context.Object;
    }

    private static User CreateUser(string name, UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        FullName = name,
        Phone = "09" + Random.Shared.Next(10000000, 99999999),
        PasswordHash = "hash",
        Role = role,
        Status = UserStatus.Active,
    };

    /// <summary>Giờ hiện tại theo giờ địa phương phòng khám — cùng hệ với SlotDate/StartTime.</summary>
    private static DateTime NowLocal() => DateTime.UtcNow + ClinicClock.Offset;

    /// <summary>
    /// Tạo lịch hẹn có giờ khám (giờ địa phương) = <paramref name="slotLocal"/>. Hồ sơ gắn với
    /// <paramref name="profileUser"/>; null = hồ sơ guest (người thân chưa có tài khoản).
    /// </summary>
    private Appointment SeedAppointment(
        DateTime slotLocal,
        AppointmentStatus status = AppointmentStatus.Booked,
        User? profileUser = null,
        Guid? bookedByUserId = null)
    {
        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = profileUser?.UserId,
            CreatedBy = Guid.NewGuid(),
        };
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctor.UserId,
            SlotDate = DateOnly.FromDateTime(slotLocal),
            StartTime = TimeOnly.FromDateTime(slotLocal),
            EndTime = TimeOnly.FromDateTime(slotLocal.AddMinutes(30)),
            Status = SlotStatus.Booked,
        };
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = profile.PatientProfileId,
            BookedByUserId = bookedByUserId,
            Status = status,
            Reason = "Checkup",
        };

        if (profileUser != null) _db.Users.Add(profileUser);
        _db.PatientProfiles.Add(profile);
        _db.ScheduleSlots.Add(slot);
        _db.Appointments.Add(appointment);
        _db.SaveChanges();
        return appointment;
    }

    private void VerifyNothingSent() =>
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

    #endregion

    #region TC-001: No Appointments

    [Fact]
    public async Task Execute_NoAppointments_DoesNothing()
    {
        await _sut.Execute(CreateMockJobExecutionContext());

        VerifyNothingSent();
    }

    #endregion

    #region TC-002: Has Upcoming Appointment Within Window

    [Fact]
    public async Task Execute_HasUpcomingAppointment_SendsNotificationWithLocalSlotTime()
    {
        // Arrange — lịch khám còn 22 giờ nữa
        var patient = CreateUser("Patient Test", UserRole.Patient);
        var slotLocal = NowLocal().AddHours(22);
        SeedAppointment(slotLocal, profileUser: patient);

        // Act
        await _sut.Execute(CreateMockJobExecutionContext());

        // Assert — nội dung in đúng giờ khám địa phương, không cộng thêm 7 tiếng
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == patient.UserId &&
                r.Type == "appointment_reminder" &&
                r.Body.Contains($"lúc {slotLocal:HH:mm}")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TC-003: Appointment Too Far in Future

    [Fact]
    public async Task Execute_AppointmentTooFar_DoesNotSendNotification()
    {
        SeedAppointment(NowLocal().AddHours(48), profileUser: CreateUser("Patient Test", UserRole.Patient));

        await _sut.Execute(CreateMockJobExecutionContext());

        VerifyNothingSent();
    }

    #endregion

    #region TC-004: Cancelled Appointment

    [Fact]
    public async Task Execute_CancelledAppointment_DoesNotSendNotification()
    {
        SeedAppointment(NowLocal().AddHours(22), AppointmentStatus.Cancelled,
            profileUser: CreateUser("Patient Test", UserRole.Patient));

        await _sut.Execute(CreateMockJobExecutionContext());

        VerifyNothingSent();
    }

    #endregion

    #region TC-005: Already Completed (Checked In)

    [Fact]
    public async Task Execute_AlreadyCompleted_DoesNotSendNotification()
    {
        SeedAppointment(NowLocal().AddHours(22), AppointmentStatus.Completed,
            profileUser: CreateUser("Patient Test", UserRole.Patient));

        await _sut.Execute(CreateMockJobExecutionContext());

        VerifyNothingSent();
    }

    #endregion

    #region TC-006: Appointment Too Soon (Less Than 20 Hours)

    [Fact]
    public async Task Execute_AppointmentTooSoon_DoesNotSendNotification()
    {
        SeedAppointment(NowLocal().AddHours(10), profileUser: CreateUser("Patient Test", UserRole.Patient));

        await _sut.Execute(CreateMockJobExecutionContext());

        // Không gửi notification vì đã quá gần rồi (sẽ có job khác nhắc sát giờ hơn)
        VerifyNothingSent();
    }

    #endregion

    #region TC-007: Slot Time Is Clinic Local Time, Not UTC

    [Fact]
    public async Task Execute_SlotClockEqualsUtcPlus22h_IsOnly15hAway_DoesNotSendNotification()
    {
        // Arrange — giờ slot trùng "UTC + 22h" nhưng giờ slot là giờ địa phương (UTC+7), nên thực tế
        // chỉ còn 15 giờ. Bản cũ so giờ địa phương với UTC nên nhắc nhầm lịch này.
        SeedAppointment(DateTime.UtcNow.AddHours(22), profileUser: CreateUser("Patient Test", UserRole.Patient));

        await _sut.Execute(CreateMockJobExecutionContext());

        VerifyNothingSent();
    }

    #endregion

    #region TC-008: Booked For Relative

    [Fact]
    public async Task Execute_BookedForGuestRelative_RemindsBooker()
    {
        // Arrange — người thân chưa có tài khoản (hồ sơ guest), lịch do người khác đặt hộ
        var booker = CreateUser("Booker", UserRole.Patient);
        _db.Users.Add(booker);
        _db.SaveChanges();
        SeedAppointment(NowLocal().AddHours(22), profileUser: null, bookedByUserId: booker.UserId);

        await _sut.Execute(CreateMockJobExecutionContext());

        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == booker.UserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_BookedForRelativeWithAccount_RemindsBookerNotProfileOwner()
    {
        var booker = CreateUser("Booker", UserRole.Patient);
        var relative = CreateUser("Relative", UserRole.Patient);
        _db.Users.Add(booker);
        _db.SaveChanges();
        SeedAppointment(NowLocal().AddHours(22), profileUser: relative, bookedByUserId: booker.UserId);

        await _sut.Execute(CreateMockJobExecutionContext());

        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == booker.UserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == relative.UserId),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
