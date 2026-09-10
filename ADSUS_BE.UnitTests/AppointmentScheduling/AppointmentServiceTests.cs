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
/// Unit tests for AppointmentService (Module 8 - UC-13, UC-14).
/// BR-01: Slot phải tồn tại và có status = OPEN; chỉ patient sở hữu mới được hủy.
/// BR-02: Patient chỉ thấy slot OPEN; không đặt trùng; lý do hủy bắt buộc.
/// </summary>
public class AppointmentServiceTests : IDisposable
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
    private readonly Guid _otherPatientId = Guid.NewGuid();
    private readonly Guid _slotId = Guid.NewGuid();
    private readonly Guid _appointmentId = Guid.NewGuid();

    public AppointmentServiceTests()
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

    #region ListOpenSlotsAsync Tests

    [Fact]
    public async Task ListOpenSlotsAsync_NoFilters_ReturnsOpenSlotsOnly()
    {
        // Arrange — ListOpenSlotsAsync now delegates the Status=Open + range filtering to
        // _slotRepo.ListByRangeAsync (repository layer), so the mock returns exactly what a
        // real Status=Open query would: only the open slot, never the booked one.
        var doctor = CreateDoctor();
        var openSlot = CreateScheduleSlot(SlotStatus.Open, doctor);

        _slotRepo.Setup(r => r.ListByRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot> { openSlot });

        // Act
        var result = await _sut.ListOpenSlotsAsync(ct: TestContext.Current.CancellationToken);

        // Assert
        var slot = Assert.Single(result);
        Assert.Equal(openSlot.SlotId, slot.SlotId);
    }

    [Fact]
    public async Task ListOpenSlotsAsync_FilterByDoctorId_ReturnsFilteredSlots()
    {
        // Arrange — mock only matches the exact doctorId the service is expected to pass
        // through, proving ListOpenSlotsAsync correctly parses and forwards it.
        var doctor1 = CreateDoctor("Dr. Smith", Guid.NewGuid());
        var slot1 = CreateScheduleSlot(SlotStatus.Open, doctor1);

        _slotRepo.Setup(r => r.ListByRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), doctor1.UserId, It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot> { slot1 });

        // Act
        var result = await _sut.ListOpenSlotsAsync(doctorId: doctor1.UserId.ToString(), ct: TestContext.Current.CancellationToken);

        // Assert
        var slot = Assert.Single(result);
        Assert.Equal(doctor1.FullName, slot.DoctorName);
    }

    [Fact]
    public async Task ListOpenSlotsAsync_FilterByDateRange_ReturnsFilteredSlots()
    {
        // Arrange — mock only matches the exact date range the service is expected to pass
        // through, proving ListOpenSlotsAsync correctly forwards fromDate/toDate.
        var doctor = CreateDoctor();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var slotFuture = CreateScheduleSlot(SlotStatus.Open, doctor, futureDate);

        _slotRepo.Setup(r => r.ListByRangeAsync(futureDate, futureDate, It.IsAny<Guid?>(), It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot> { slotFuture });

        // Act
        var result = await _sut.ListOpenSlotsAsync(
            fromDate: futureDate,
            toDate: futureDate,
            ct: TestContext.Current.CancellationToken);

        // Assert
        var slot = Assert.Single(result);
        Assert.Equal(futureDate, slot.SlotDate);
    }

    [Fact]
    public async Task ListOpenSlotsAsync_SlotWithBookedAppointment_Excluded()
    {
        // Arrange - slot has BOOKED appointment. This exclusion still happens in-memory in
        // ListOpenSlotsAsync after fetching from the repository, so it's still worth testing
        // here directly.
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.Appointments = new List<Appointment>
        {
            new()
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                PatientProfileId = _patientId,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
            }
        };

        _slotRepo.Setup(r => r.ListByRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot> { slot });

        // Act
        var result = await _sut.ListOpenSlotsAsync(ct: TestContext.Current.CancellationToken);

        // Assert - slot with BOOKED appointment should be excluded
        Assert.Empty(result);
    }

    #endregion

    #region ListMyAppointmentsAsync Tests

    [Fact]
    public async Task ListMyAppointmentsAsync_ReturnsPatientAppointments()
    {
        // Arrange
        SetupPatientAppointmentRepository();

        // Act
        var result = await _sut.ListMyAppointmentsAsync(_patientId, ct: TestContext.Current.CancellationToken);

        // Assert
        var appointment = Assert.Single(result);
        Assert.Equal(_appointmentId, appointment.AppointmentId);
    }

    [Fact]
    public async Task ListMyAppointmentsAsync_FilterByStatus_ReturnsFilteredAppointments()
    {
        // Arrange
        SetupPatientAppointmentRepository();

        // Act
        var result = await _sut.ListMyAppointmentsAsync(
            _patientId,
            statusFilter: AppointmentStatus.Booked,
            ct: TestContext.Current.CancellationToken);

        // Assert
        var appointment = Assert.Single(result);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    [Fact]
    public async Task ListMyAppointmentsAsync_FilterByCancelledStatus_ReturnsEmpty()
    {
        // Arrange
        SetupPatientAppointmentRepository();

        // Act
        var result = await _sut.ListMyAppointmentsAsync(
            _patientId,
            statusFilter: AppointmentStatus.Cancelled,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingAppointment_ReturnsAppointment()
    {
        // Arrange
        SetupGetByIdRepository();

        // Act
        var result = await _sut.GetByIdAsync(_appointmentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_appointmentId, result!.AppointmentId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentAppointment_ReturnsNull()
    {
        // Arrange
        _appointmentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region BookAppointmentAsync Tests

    [Fact]
    public async Task BookAppointmentAsync_ValidSlot_CreatesAppointment()
    {
        // Arrange
        var slot = SetupBookSlotScenario();

        // Act
        var result = await _sut.BookAppointmentAsync(_patientId,
            new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.Equal(_slotId, result.ScheduleSlotId);
    }

    [Fact]
    public async Task BookAppointmentAsync_SlotNotFound_ThrowsException()
    {
        // Arrange
        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.BookAppointmentAsync(_patientId,
                new BookAppointmentRequest { ScheduleSlotId = Guid.NewGuid() }, TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task BookAppointmentAsync_SlotNotOpen_ThrowsException()
    {
        // Arrange
        var slot = new ScheduleSlot
        {
            SlotId = _slotId,
            DoctorId = _doctorId,
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Closed, // Not Open
            CreatedAt = DateTime.UtcNow,
            Appointments = new List<Appointment>(),
        };

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.BookAppointmentAsync(_patientId,
                new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken));

        Assert.Contains("không còn nhận đặt lịch", ex.Message);
    }

    [Fact]
    public async Task BookAppointmentAsync_SlotAlreadyBooked_ThrowsException()
    {
        // Arrange
        var slot = new ScheduleSlot
        {
            SlotId = _slotId,
            DoctorId = _doctorId,
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Open,
            Appointments = new List<Appointment>
            {
                new()
                {
                    AppointmentId = Guid.NewGuid(),
                    SlotId = _slotId,
                    PatientProfileId = _otherPatientId,
                    Status = AppointmentStatus.Booked,
                    CreatedAt = DateTime.UtcNow,
                }
            },
        };

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.BookAppointmentAsync(_patientId,
                new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken));

        Assert.Contains("đã có người đặt", ex.Message);
    }

    [Fact]
    public async Task BookAppointmentAsync_SuccessfulBooking_SlotStatusUpdatedToBooked()
    {
        // Arrange
        var slot = SetupBookSlotScenario();

        // Act
        await _sut.BookAppointmentAsync(_patientId,
            new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SlotStatus.Booked, slot.Status);
        _slotRepo.Verify(r => r.UpdateAsync(It.Is<ScheduleSlot>(s => s.Status == SlotStatus.Booked), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookAppointmentAsync_PatientHasThreeCompletedAppointments_StillAllowsBooking()
    {
        // Arrange — this is a forward regression guard, NOT RED->GREEN evidence for the
        // Booked-only fix itself: Completed was never part of the old "Booked || Approved"
        // condition, so this test passes against both the pre-fix and post-fix code. It
        // guards against a different, previously-rejected change — a mechanical
        // Approved->Completed rename that would make Completed count toward Rule 1 forever.
        // See BookAppointmentAsync_PatientHasThreeApprovedAppointments_StillAllowsBooking
        // below for the genuine RED->GREEN evidence of this diff.
        var slot = SetupBookSlotScenario();

        for (var i = 0; i < 3; i++)
        {
            var doctor = CreateDoctor($"Dr. Other {i}", Guid.NewGuid());
            var completedSlot = CreateScheduleSlot(SlotStatus.Booked, doctor, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10 - i)));
            _db.ScheduleSlots.Add(completedSlot);
            _db.Appointments.Add(new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = completedSlot.SlotId,
                PatientProfileId = _patientId,
                Status = AppointmentStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        _db.SaveChanges();

        // Act
        var result = await _sut.BookAppointmentAsync(_patientId,
            new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    [Fact]
    public async Task BookAppointmentAsync_PatientHasCompletedAppointmentSameDay_StillAllowsBooking()
    {
        // Arrange — this is a forward regression guard, NOT RED->GREEN evidence for the
        // Booked-only fix itself: Completed was never part of the old "Booked || Approved"
        // condition, so this test passes against both the pre-fix and post-fix code. It
        // guards against a different, previously-rejected change — a mechanical
        // Approved->Completed rename that would make Completed count toward Rule 3 forever.
        // See BookAppointmentAsync_PatientHasApprovedAppointmentSameDay_StillAllowsBooking
        // below for the genuine RED->GREEN evidence of this diff.
        var slot = SetupBookSlotScenario();
        var doctor = CreateDoctor("Dr. Other", Guid.NewGuid());

        var sameDaySlot = CreateScheduleSlot(SlotStatus.Booked, doctor, slot.SlotDate);
        _db.ScheduleSlots.Add(sameDaySlot);
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = sameDaySlot.SlotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        // Act
        var result = await _sut.BookAppointmentAsync(_patientId,
            new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    [Fact]
    public async Task BookAppointmentAsync_PatientHasCompletedAppointmentWithin3Days_StillAllowsBooking()
    {
        // Arrange — this is a forward regression guard, NOT RED->GREEN evidence for the
        // Booked-only fix itself: Completed was never part of the old "Booked || Approved"
        // condition, so this test passes against both the pre-fix and post-fix code. It
        // guards against a different, previously-rejected change — a mechanical
        // Approved->Completed rename that would make Completed count toward Rule 2 forever.
        // See BookAppointmentAsync_PatientHasApprovedAppointmentWithin3Days_StillAllowsBooking
        // below for the genuine RED->GREEN evidence of this diff.
        var slot = SetupBookSlotScenario();
        var doctor = CreateDoctor("Dr. Other", Guid.NewGuid());

        var withinWindowSlot = CreateScheduleSlot(SlotStatus.Booked, doctor, slot.SlotDate.AddDays(2));
        _db.ScheduleSlots.Add(withinWindowSlot);
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = withinWindowSlot.SlotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        // Act
        var result = await _sut.BookAppointmentAsync(_patientId,
            new BookAppointmentRequest { ScheduleSlotId = _slotId }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    #endregion

    #region CreateFollowUpAppointmentAsync Tests

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_Success_CreatesFollowUp()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = _slotId;

        var patientProfile = new PatientProfile
        {
            PatientProfileId = _patientId,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.ScheduleSlots.Add(slot);
        _db.PatientProfiles.Add(patientProfile);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment a, CancellationToken _) => a);
        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _profileRepo.Setup(r => r.GetByIdAsync(patientProfile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patientProfile);

        var request = new FollowUpAppointmentRequest
        {
            ScheduleSlotId = _slotId,
            PatientProfileId = _patientId,
            Reason = "Tái khám"
        };

        // Act
        var result = await _sut.CreateFollowUpAppointmentAsync(doctor.UserId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.Equal(SlotStatus.Booked, slot.Status);
        Assert.Equal("Tái khám", result.Reason);

        // Verify notification sent to both patient and doctor
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r => r.UserId == patientProfile.UserId && r.Type == "appointment_booking" && r.Title == "Lịch hẹn tái khám"),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r => r.UserId == doctor.UserId && r.Type == "appointment_booking" && r.Title == "Tạo lịch tái khám thành công"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_NotificationThrows_DoesNotFailAppointment()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = Guid.NewGuid();

        var patientProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.ScheduleSlots.Add(slot);
        _db.PatientProfiles.Add(patientProfile);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment a, CancellationToken _) => a);
        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _profileRepo.Setup(r => r.GetByIdAsync(patientProfile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patientProfile);

        // Make notification service throw an exception to verify best-effort resilience
        _notificationService.Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification gateway network error"));

        var request = new FollowUpAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            PatientProfileId = patientProfile.PatientProfileId,
            Reason = "Tái khám sau phẫu thuật"
        };

        // Act
        var result = await _sut.CreateFollowUpAppointmentAsync(doctor.UserId, request, TestContext.Current.CancellationToken);

        // Assert: Appointment is still successfully booked despite notification failure
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.Equal(SlotStatus.Booked, slot.Status);
        Assert.Equal("Tái khám sau phẫu thuật", result.Reason);
    }

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_SlotNotFound_ThrowsException()
    {
        // Arrange
        var doctorId = Guid.NewGuid();
        var request = new FollowUpAppointmentRequest { ScheduleSlotId = Guid.NewGuid() };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.CreateFollowUpAppointmentAsync(doctorId, request, TestContext.Current.CancellationToken));
        
        Assert.Contains("Không tìm thấy khung giờ này", ex.Message);
    }

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_SlotNotBelongingToDoctor_ThrowsException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var otherDoctor = CreateDoctor("Other Doctor", Guid.NewGuid());
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = _slotId;

        _db.ScheduleSlots.Add(slot);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new FollowUpAppointmentRequest { ScheduleSlotId = _slotId };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateFollowUpAppointmentAsync(otherDoctor.UserId, request, TestContext.Current.CancellationToken));
        
        Assert.Contains("Chỉ được chọn khung giờ của chính bạn", ex.Message);
    }

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_SlotNotOpen_ThrowsException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor);
        slot.SlotId = _slotId;

        _db.ScheduleSlots.Add(slot);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new FollowUpAppointmentRequest { ScheduleSlotId = _slotId };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateFollowUpAppointmentAsync(doctor.UserId, request, TestContext.Current.CancellationToken));
        
        Assert.Contains("đã được đặt hoặc đã đóng", ex.Message);
    }

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_PatientHasConflict_ThrowsException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = _slotId;

        var existingAppointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = _slotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ScheduleSlots.Add(slot);
        _db.Appointments.Add(existingAppointment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new FollowUpAppointmentRequest
        {
            ScheduleSlotId = _slotId,
            PatientProfileId = _patientId
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateFollowUpAppointmentAsync(doctor.UserId, request, TestContext.Current.CancellationToken));
        
        Assert.Contains("Bệnh nhân đã có lịch hẹn trong khung giờ này", ex.Message);
    }

    [Fact]
    public async Task CreateFollowUpAppointmentAsync_PatientProfileNotFound_ThrowsException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = _slotId;

        _db.ScheduleSlots.Add(slot);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new FollowUpAppointmentRequest
        {
            ScheduleSlotId = _slotId,
            PatientProfileId = _patientId
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.CreateFollowUpAppointmentAsync(doctor.UserId, request, TestContext.Current.CancellationToken));
        
        Assert.Contains("Không tìm thấy hồ sơ bệnh nhân", ex.Message);
    }

    #endregion

    #region CancelAppointmentAsync Tests

    [Fact]
    public async Task CancelAppointmentAsync_ValidCancellation_CancelsAppointment()
    {
        // Arrange
        var appointment = SetupCancelScenario();

        // Act
        var result = await _sut.CancelAppointmentAsync(
            _appointmentId,
            _patientId,
            new CancelAppointmentRequest { CancellationReason = "Schedule conflict" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
        Assert.Equal("Schedule conflict", result.CancellationReason);
    }

    [Fact]
    public async Task CancelAppointmentAsync_EmptyReason_ThrowsException()
    {
        // Arrange
        var appointment = SetupCancelScenario();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelAppointmentAsync(
                _appointmentId,
                _patientId,
                new CancelAppointmentRequest { CancellationReason = "" },
                TestContext.Current.CancellationToken));

        Assert.Contains("bắt buộc", ex.Message);
    }

    [Fact]
    public async Task CancelAppointmentAsync_WhitespaceReason_ThrowsException()
    {
        // Arrange
        var appointment = SetupCancelScenario();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelAppointmentAsync(
                _appointmentId,
                _patientId,
                new CancelAppointmentRequest { CancellationReason = "   " },
                TestContext.Current.CancellationToken));

        Assert.Contains("bắt buộc", ex.Message);
    }

    [Fact]
    public async Task CancelAppointmentAsync_AppointmentNotFound_ThrowsException()
    {
        // Arrange
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = _appointmentId,
            SlotId = _slotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelAppointmentAsync(
                Guid.NewGuid(), // Different ID
                _patientId,
                new CancelAppointmentRequest { CancellationReason = "Test" },
                TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task CancelAppointmentAsync_NotOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var appointment = SetupCancelScenario();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CancelAppointmentAsync(
                _appointmentId,
                _otherPatientId, // Different patient
                new CancelAppointmentRequest { CancellationReason = "Test" },
                TestContext.Current.CancellationToken));

        Assert.Contains("không có quyền", ex.Message);
    }

    [Fact]
    public async Task CancelAppointmentAsync_AlreadyCancelled_ThrowsException()
    {
        // Arrange
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);

        var appointment = new Appointment
        {
            AppointmentId = _appointmentId,
            SlotId = _slotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Cancelled, // Already cancelled
            CancelledReason = "Previous cancellation",
            CreatedAt = DateTime.UtcNow,
            Slot = slot,
        };

        _db.Appointments.Add(appointment);
        _db.ScheduleSlots.Add(slot);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CancelAppointmentAsync(
                _appointmentId,
                _patientId,
                new CancelAppointmentRequest { CancellationReason = "Test" },
                TestContext.Current.CancellationToken));

        Assert.Contains("đang đặt mới được hủy", ex.Message);
    }

    [Fact]
    public async Task CancelAppointmentAsync_SuccessfulCancel_SlotReopened()
    {
        // Arrange
        var appointment = SetupCancelScenario();

        // Act
        await _sut.CancelAppointmentAsync(
            _appointmentId,
            _patientId,
            new CancelAppointmentRequest { CancellationReason = "Schedule conflict" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SlotStatus.Open, appointment.Slot!.Status);
    }

    #endregion

    #region ListForDoctorAsync Tests

    [Fact]
    public async Task ListForDoctorAsync_ExcludesCancelled()
    {
        var fromDate = new DateOnly(2026, 7, 10);
        var toDate = new DateOnly(2026, 7, 16);

        var patientUser = new User { UserId = Guid.NewGuid(), FullName = "Trần Văn Bình" };
        var patientProfile = new PatientProfile { PatientProfileId = Guid.NewGuid(), User = patientUser };
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctorId,
            SlotDate = new DateOnly(2026, 7, 11),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
        };

        var bookedAppointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientProfile.PatientProfileId,
            PatientProfile = patientProfile,
            Status = AppointmentStatus.Booked,
            Reason = "Khám định kỳ",
        };
        var cancelledAppointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientProfile.PatientProfileId,
            PatientProfile = patientProfile,
            Status = AppointmentStatus.Cancelled,
        };

        _appointmentRepo
            .Setup(r => r.ListByDoctorAsync(_doctorId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { bookedAppointment, cancelledAppointment });

        var result = await _sut.ListForDoctorAsync(_doctorId, fromDate, toDate, TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(bookedAppointment.AppointmentId, item.AppointmentId);
        Assert.Equal("Trần Văn Bình", item.PatientFullName);
        Assert.Equal("Khám định kỳ", item.Reason);
        Assert.Equal(slot.SlotDate, item.SlotDate);
    }

    [Fact]
    public async Task ListForDoctorAsync_NoAppointments_ReturnsEmptyList()
    {
        var fromDate = new DateOnly(2026, 7, 10);
        var toDate = new DateOnly(2026, 7, 16);

        _appointmentRepo
            .Setup(r => r.ListByDoctorAsync(_doctorId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Appointment>());

        var result = await _sut.ListForDoctorAsync(_doctorId, fromDate, toDate, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListForDoctorAsync_CompletedAppointment_IsExcluded()
    {
        // Arrange — appointment is already completed (doctor ended the case) and should NOT
        // show up in the doctor's schedule list for future/current appointments.
        var fromDate = new DateOnly(2026, 7, 10);
        var toDate = new DateOnly(2026, 7, 16);

        var patientUser = new User { UserId = Guid.NewGuid(), FullName = "Nguyễn Văn Công" };
        var patientProfile = new PatientProfile { PatientProfileId = Guid.NewGuid(), User = patientUser };
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctorId,
            SlotDate = new DateOnly(2026, 7, 11),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
        };

        var completedAppointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = patientProfile.PatientProfileId,
            PatientProfile = patientProfile,
            Status = AppointmentStatus.Completed,  // Doctor already ended case
            Reason = "Khám tổng quát",
        };

        _appointmentRepo
            .Setup(r => r.ListByDoctorAsync(_doctorId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { completedAppointment });

        // Act
        var result = await _sut.ListForDoctorAsync(_doctorId, fromDate, toDate, TestContext.Current.CancellationToken);

        // Assert — Completed appointment should be excluded
        Assert.Empty(result);
    }

    #endregion

    #region Helper Methods

    private User CreateDoctor(string name = "Dr. Test", Guid? specificId = null)
    {
        var doctor = new User
        {
            UserId = specificId ?? Guid.NewGuid(),
            FullName = name,
            Email = $"{name.ToLower().Replace(" ", ".")}@test.com",
            Phone = "1234567890",
            PasswordHash = "hash-for-testing-only",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(doctor);
        return doctor;
    }

    private ScheduleSlot CreateScheduleSlot(SlotStatus status, User doctor, DateOnly? date = null)
    {
        var doctorEntry = _db.Entry(doctor);
        doctorEntry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Appointments = new List<Appointment>(),
        };
        return slot;
    }

    private void SetupPatientAppointmentRepository()
    {
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = _slotId;

        var appointment = new Appointment
        {
            AppointmentId = _appointmentId,
            SlotId = _slotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            Slot = slot,
        };

        _db.ScheduleSlots.Add(slot);
        _db.Appointments.Add(appointment);
        _db.SaveChanges();

        _appointmentRepo.Setup(r => r.ListByPatientAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });
    }

    private void SetupGetByIdRepository()
    {
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Open, doctor);
        slot.SlotId = _slotId;

        var appointment = new Appointment
        {
            AppointmentId = _appointmentId,
            SlotId = _slotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            Slot = slot,
        };

        _db.ScheduleSlots.Add(slot);
        _db.Appointments.Add(appointment);
        _db.SaveChanges();

        _appointmentRepo.Setup(r => r.GetByIdAsync(_appointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
    }

    private ScheduleSlot SetupBookSlotScenario()
    {
        var slot = new ScheduleSlot
        {
            SlotId = _slotId,
            DoctorId = _doctorId,
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Open,
            CreatedAt = DateTime.UtcNow,
            Appointments = new List<Appointment>(),
            Doctor = new User
            {
                UserId = _doctorId,
                FullName = "Dr. Test",
                Email = "dr.test@test.com",
                Phone = "1234567890",
                PasswordHash = "hash",
                Role = UserRole.Doctor,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
            }
        };

        var patientProfile = new PatientProfile
        {
            PatientProfileId = _patientId,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment a, CancellationToken _) => a);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _profileRepo.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patientProfile);

        _notificationService.Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        return slot;
    }

    private Appointment SetupCancelScenario()
    {
        var doctor = CreateDoctor();
        var slot = CreateScheduleSlot(SlotStatus.Booked, doctor);
        slot.SlotId = _slotId;

        var appointment = new Appointment
        {
            AppointmentId = _appointmentId,
            SlotId = _slotId,
            PatientProfileId = _patientId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Slot = slot,
        };

        var patientProfile = new PatientProfile
        {
            PatientProfileId = _patientId,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.Appointments.Add(appointment);
        _db.ScheduleSlots.Add(slot);
        _db.SaveChanges();

        _profileRepo.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patientProfile);

        _notificationService.Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        return appointment;
    }

    #endregion
}
