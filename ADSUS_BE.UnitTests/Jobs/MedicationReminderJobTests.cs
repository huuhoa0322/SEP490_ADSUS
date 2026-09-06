using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ADSUS_BE.UnitTests.Jobs;

/// <summary>
/// Unit tests cho MedicationReminderJob (JOB-01).
/// Gửi nhắc nhở uống thuốc mỗi 30 phút.
/// </summary>
public class MedicationReminderJobTests : IDisposable
{
    private readonly Mock<IMedicationIntakeLogRepository> _intakeLogRepo = new();
    private readonly Mock<IPatientProfileRepository> _patientProfileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ILogger<MedicationReminderJob>> _logger = new();
    private readonly AppDbContext _db;
    private readonly MedicationReminderJob _sut;

    public MedicationReminderJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(_db);
        serviceProvider.Setup(sp => sp.GetService(typeof(INotificationService))).Returns(_notificationService.Object);
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        _sut = new MedicationReminderJob(
            scopeFactory.Object,
            _intakeLogRepo.Object,
            _patientProfileRepo.Object,
            _logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region Helper Methods

    private User CreatePatient(string name = "Patient Test")
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = name,
            Phone = "0900000001",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
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

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var context = new Mock<IJobExecutionContext>();
        return context.Object;
    }

    #endregion

    #region TC-001: No Pending Reminders

    [Fact]
    public async Task Execute_NoPendingReminders_DoesNothing()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-30);
        _intakeLogRepo.Setup(r => r.ListDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicationIntakeLog>());

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region TC-002: Has Pending Reminders

    [Fact]
    public async Task Execute_HasPendingReminders_SendsNotifications()
    {
        // Arrange
        var patient = CreatePatient();
        var profile = CreatePatientProfile(patient);

        var intakeLog = new MedicationIntakeLog
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddMinutes(-10),
            ConfirmedAt = null,
            PrescriptionItem = new PrescriptionItem
            {
                PrescriptionItemId = Guid.NewGuid(),
                Medicine = new Medicine { Name = "Paracetamol" },
                Prescription = new Prescription
                {
                    Case = new Case { PatientProfileId = profile.PatientProfileId, PatientProfile = profile }
                }
            }
        };

        _intakeLogRepo.Setup(r => r.ListDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicationIntakeLog> { intakeLog });

        _patientProfileRepo.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Notification được gửi
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r =>
                r.UserId == patient.UserId &&
                r.Type == "medication_reminder"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region TC-003: No FCM Token

    [Fact]
    public async Task Execute_NoFcmToken_DoesNotSendNotification()
    {
        // Arrange
        var intakeLog = new MedicationIntakeLog
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddMinutes(-10),
            ConfirmedAt = null,
            PrescriptionItem = new PrescriptionItem
            {
                PrescriptionItemId = Guid.NewGuid(),
                Medicine = new Medicine { Name = "Paracetamol" },
                Prescription = new Prescription
                {
                    Case = new Case { PatientProfileId = Guid.NewGuid() }
                }
            }
        };

        _intakeLogRepo.Setup(r => r.ListDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicationIntakeLog> { intakeLog });

        _patientProfileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientProfile?)null);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Không có notification được gửi
        _notificationService.Verify(
            s => s.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region TC-004: Notification Failure Continues

    [Fact]
    public async Task Execute_NotificationFails_ContinuesProcessingOtherReminders()
    {
        // Arrange
        var patient1 = CreatePatient("Patient 1");
        var patient2 = CreatePatient("Patient 2");
        var profile1 = CreatePatientProfile(patient1);
        var profile2 = CreatePatientProfile(patient2);

        var intakeLog1 = new MedicationIntakeLog
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddMinutes(-10),
            ConfirmedAt = null,
            PrescriptionItem = new PrescriptionItem
            {
                PrescriptionItemId = Guid.NewGuid(),
                Medicine = new Medicine { Name = "Paracetamol" },
                Prescription = new Prescription
                {
                    Case = new Case { PatientProfileId = profile1.PatientProfileId, PatientProfile = profile1 }
                }
            }
        };

        var intakeLog2 = new MedicationIntakeLog
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = Guid.NewGuid(),
            ScheduledTime = DateTime.UtcNow.AddMinutes(-10),
            ConfirmedAt = null,
            PrescriptionItem = new PrescriptionItem
            {
                PrescriptionItemId = Guid.NewGuid(),
                Medicine = new Medicine { Name = "Aspirin" },
                Prescription = new Prescription
                {
                    Case = new Case { PatientProfileId = profile2.PatientProfileId, PatientProfile = profile2 }
                }
            }
        };

        _intakeLogRepo.Setup(r => r.ListDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicationIntakeLog> { intakeLog1, intakeLog2 });

        _patientProfileRepo.Setup(r => r.GetByIdAsync(profile1.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile1);
        _patientProfileRepo.Setup(r => r.GetByIdAsync(profile2.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile2);

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

        // Act - Không throw
        await _sut.Execute(context);

        // Assert - Vẫn tiếp tục gửi cho patient2
        _notificationService.Verify(
            s => s.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == patient2.UserId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
