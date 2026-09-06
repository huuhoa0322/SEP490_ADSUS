using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ADSUS_BE.UnitTests.Jobs;

/// <summary>
/// Unit tests cho AppointmentReminderJob (JOB-03).
/// Gửi nhắc nhở lịch hẹn trước 24 giờ.
/// </summary>
public class AppointmentReminderJobTests
{
    private readonly Mock<IPatientProfileRepository> _patientProfileRepo = new();
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ILogger<AppointmentReminderJob>> _logger = new();
    private readonly AppointmentReminderJob _sut;

    public AppointmentReminderJobTests()
    {
        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(INotificationService))).Returns(_notificationService.Object);
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        _sut = new AppointmentReminderJob(
            scopeFactory.Object,
            _patientProfileRepo.Object,
            _appointmentRepo.Object,
            _logger.Object);
    }

    #region Helper Methods

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var context = new Mock<IJobExecutionContext>();
        return context.Object;
    }

    private PatientListRow CreatePatientListRow(Guid userId, Guid? profileId)
    {
        return new PatientListRow(
            PatientProfileId: profileId,
            PatientUserId: userId,
            FullName: "Patient Test",
            Phone: "0900000001",
            LatestVisitDate: null,
            LatestVisitStatus: null);
    }

    private Appointment CreateAppointment(Guid slotId, AppointmentStatus status, DateTime slotDateTime)
    {
        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = Guid.NewGuid(),
            Doctor = new User { FullName = "Dr. Test" },
            SlotDate = DateOnly.FromDateTime(slotDateTime),
            StartTime = TimeOnly.FromDateTime(slotDateTime),
            EndTime = TimeOnly.FromDateTime(slotDateTime.AddMinutes(30)),
            Status = SlotStatus.Booked,
        };

        return new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slotId,
            Slot = slot,
            PatientProfileId = Guid.NewGuid(),
            Status = status,
            Reason = "Checkup",
        };
    }

    #endregion

    #region TC-001: No Patients

    [Fact]
    public async Task Execute_NoPatients_DoesNothing()
    {
        // Arrange
        _patientProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow>(), 0));

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region TC-002: Has Upcoming Appointment Within Window

    [Fact]
    public async Task Execute_HasUpcomingAppointment_SendsNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        // Appointment trong vòng 20-24 giờ tới
        var slotTime = DateTime.UtcNow.AddHours(22);
        var patient = CreatePatientListRow(userId, profileId);
        var appointment = CreateAppointment(slotId, AppointmentStatus.Booked, slotTime);

        _patientProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow> { patient }, 1));

        _appointmentRepo.Setup(r => r.ListByPatientAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == userId &&
                r.Type == "appointment_reminder"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TC-003: Appointment Too Far in Future

    [Fact]
    public async Task Execute_AppointmentTooFar_DoesNotSendNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        // Appointment cách 2 ngày (48 giờ)
        var slotTime = DateTime.UtcNow.AddHours(48);
        var patient = CreatePatientListRow(userId, profileId);
        var appointment = CreateAppointment(slotId, AppointmentStatus.Booked, slotTime);

        _patientProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow> { patient }, 1));

        _appointmentRepo.Setup(r => r.ListByPatientAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region TC-004: Cancelled Appointment

    [Fact]
    public async Task Execute_CancelledAppointment_DoesNotSendNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var slotTime = DateTime.UtcNow.AddHours(22);
        var patient = CreatePatientListRow(userId, profileId);
        var appointment = CreateAppointment(slotId, AppointmentStatus.Cancelled, slotTime);

        _patientProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow> { patient }, 1));

        _appointmentRepo.Setup(r => r.ListByPatientAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region TC-005: Already Approved (Checked In)

    [Fact]
    public async Task Execute_AlreadyApproved_DoesNotSendNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var slotTime = DateTime.UtcNow.AddHours(22);
        var patient = CreatePatientListRow(userId, profileId);
        var appointment = CreateAppointment(slotId, AppointmentStatus.Approved, slotTime);

        _patientProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow> { patient }, 1));

        _appointmentRepo.Setup(r => r.ListByPatientAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region TC-006: Appointment Too Soon (Less Than 20 Hours)

    [Fact]
    public async Task Execute_AppointmentTooSoon_DoesNotSendNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        // Appointment chỉ còn 10 giờ nữa (dưới ngưỡng 20 giờ)
        var slotTime = DateTime.UtcNow.AddHours(10);
        var patient = CreatePatientListRow(userId, profileId);
        var appointment = CreateAppointment(slotId, AppointmentStatus.Booked, slotTime);

        _patientProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow> { patient }, 1));

        _appointmentRepo.Setup(r => r.ListByPatientAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Không gửi notification vì đã quá gần rồi (sẽ có job khác nhắc sát giờ hơn)
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
