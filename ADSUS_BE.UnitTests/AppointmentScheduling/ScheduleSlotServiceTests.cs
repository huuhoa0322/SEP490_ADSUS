using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Unit tests for ScheduleSlotService (Module 8 - UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); range > 15 phút; không overlap.
/// BR-02: Closed có thể mở lại (ReopenSlotAsync).
/// </summary>
public class ScheduleSlotServiceTests
{
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly ScheduleSlotService _sut;

    // Test data
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _slotId = Guid.NewGuid();

    public ScheduleSlotServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: "Schedule_Test").Options;
        var db = new AppDbContext(options);
        var notificationMock = new Mock<INotificationService>();
        _sut = new ScheduleSlotService(
            _slotRepo.Object,
            _userRepo.Object, notificationMock.Object, db);

        SetupDoctor();
    }

    #region ListSlotsAsync Tests

    [Fact]
    public async Task ListSlotsAsync_ValidDoctorId_ReturnsPagedSlots()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slots = new List<ScheduleSlot>
        {
            CreateSlot(futureDate, SlotStatus.Open)
        };

        _slotRepo.Setup(r => r.ListByRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                _doctorId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        var (items, totalCount) = await _sut.ListSlotsAsync(doctorId: _doctorId, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(items);
        Assert.True(totalCount >= 0);
    }

    [Fact]
    public async Task ListSlotsAsync_EmptyDoctorId_ThrowsException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ListSlotsAsync(doctorId: Guid.Empty, ct: TestContext.Current.CancellationToken));

        Assert.Contains("doctorId is required", ex.Message);
    }

    [Fact]
    public async Task ListSlotsAsync_ToDateBeforeFromDate_ThrowsException()
    {
        // Arrange
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var toDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)); // Before fromDate

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ListSlotsAsync(
                fromDate: fromDate,
                toDate: toDate,
                doctorId: _doctorId,
                ct: TestContext.Current.CancellationToken));

        Assert.Contains("toDate must not be before fromDate", ex.Message);
    }

    #endregion

    #region EnsureUpcomingSlotsAsync Tests

    [Fact]
    public async Task EnsureUpcomingSlotsAsync_ValidDoctor_AutoGeneratesSlots()
    {
        // Arrange
        _slotRepo.Setup(r => r.ListByRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                _doctorId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        await _sut.EnsureUpcomingSlotsAsync(_doctorId, TestContext.Current.CancellationToken);

        // Assert
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureUpcomingSlotsAsync_EmptyDoctorId_ThrowsException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.EnsureUpcomingSlotsAsync(Guid.Empty, TestContext.Current.CancellationToken));

        Assert.Contains("doctorId is required", ex.Message);
    }

    [Fact]
    public async Task EnsureUpcomingSlotsAsync_NotADoctor_ThrowsException()
    {
        // Arrange
        _userRepo.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                UserId = _patientId,
                FullName = "Patient User",
                Role = UserRole.Patient, // Not a Doctor
                Status = UserStatus.Active,
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.EnsureUpcomingSlotsAsync(_patientId, TestContext.Current.CancellationToken));

        Assert.Contains("not a valid Doctor", ex.Message);
    }

    #endregion

    #region GetSlotAsync Tests

    [Fact]
    public async Task GetSlotAsync_ExistingSlot_ReturnsSlot()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);

        _slotRepo.Setup(r => r.GetByIdAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act
        var result = await _sut.GetSlotAsync(_slotId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_slotId, result!.SlotId);
    }

    [Fact]
    public async Task GetSlotAsync_NonExistentSlot_ReturnsNull()
    {
        // Arrange
        _slotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot?)null);

        // Act
        var result = await _sut.GetSlotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    #endregion


    #region CloseSlotAsync Tests

    [Fact]
    public async Task CloseSlotAsync_NoBookings_ClosesSlot()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);
        slot.Appointments = new List<Appointment>(); // No bookings

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CloseSlotAsync(_slotId, forceClose: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.AffectedBookingsCount);
        Assert.Equal(SlotStatus.Closed, slot.Status);
    }

    [Fact]
    public async Task CloseSlotAsync_HasBookings_NoForce_ReturnsImpactCount()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);
        slot.Appointments = new List<Appointment>
        {
            new()
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = _slotId,
                PatientProfileId = _patientId,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
            }
        };

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act
        var result = await _sut.CloseSlotAsync(_slotId, forceClose: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.AffectedBookingsCount);
        Assert.Equal(SlotStatus.Open, slot.Status); // Not closed
    }

    [Fact]
    public async Task CloseSlotAsync_HasBookings_ForceClose_ClosesSlot()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);
        slot.Appointments = new List<Appointment>
        {
            new()
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = _slotId,
                PatientProfileId = _patientId,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
            }
        };

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CloseSlotAsync(_slotId, forceClose: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.AffectedBookingsCount);
        Assert.Equal(SlotStatus.Closed, slot.Status);
    }

    [Fact]
    public async Task CloseSlotAsync_AlreadyClosed_ThrowsException()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Closed);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CloseSlotAsync(_slotId, forceClose: false, TestContext.Current.CancellationToken));

        Assert.Contains("already closed", ex.Message);
    }

    #endregion

    #region ReopenSlotAsync Tests

    [Fact]
    public async Task ReopenSlotAsync_ClosedSlot_ReopensSlot()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Closed);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ReopenSlotAsync(_slotId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SlotStatus.Open, result.Status);
    }

    [Fact]
    public async Task ReopenSlotAsync_AlreadyOpen_ThrowsException()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ReopenSlotAsync(_slotId, TestContext.Current.CancellationToken));

        Assert.Contains("already open", ex.Message);
    }

    [Fact]
    public async Task ReopenSlotAsync_SlotNotFound_ThrowsException()
    {
        // Arrange
        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ReopenSlotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
    }

    #endregion

    #region EnsureDefaultSlotsAsync Tests

    [Fact]
    public async Task EnsureDefaultSlotsAsync_ValidWeekStart_CreatesDefaultSlots()
    {
        // Arrange - Week starting on Monday
        var weekStart = GetNextMonday();

        _userRepo.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                UserId = _doctorId,
                FullName = "Dr. Test",
                Role = UserRole.Doctor,
                Status = UserStatus.Active,
            });

        _slotRepo.Setup(r => r.HasOverlapAsync(
                _doctorId, It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(),
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        await _sut.EnsureDefaultSlotsAsync(_doctorId, weekStart, TestContext.Current.CancellationToken);

        // Assert
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureDefaultSlotsAsync_NotMonday_ThrowsException()
    {
        // Arrange - Not a Monday
        var notMonday = DateOnly.FromDateTime(DateTime.UtcNow);
        if (notMonday.DayOfWeek == DayOfWeek.Monday)
        {
            notMonday = notMonday.AddDays(1);
        }

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.EnsureDefaultSlotsAsync(_doctorId, notMonday, TestContext.Current.CancellationToken));

        Assert.Contains("weekStart must be a Monday", ex.Message);
    }

    #endregion

    #region Timezone & Overtime Slots Tests

    [Fact]
    public async Task CreateOvertimeSlotsAsync_PastVisitDateInVietnamTime_SkipsAllSlots()
    {
        // Arrange
        var pastDateVn = ClinicClock.Today().AddDays(-1);
        var request = new CreateOvertimeSlotsRequest
        {
            VisitDate = pastDateVn,
        };

        _slotRepo.Setup(r => r.ListByRangeAsync(
                pastDateVn, pastDateVn, _doctorId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        // Act
        var (successCount, errorCount) = await _sut.CreateOvertimeSlotsAsync(request, _doctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, successCount);
        Assert.Equal(6, errorCount);
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_FutureVisitDateInVietnamTime_CreatesAll6Slots()
    {
        // Arrange
        var futureDateVn = ClinicClock.Today().AddDays(2);
        var request = new CreateOvertimeSlotsRequest
        {
            VisitDate = futureDateVn,
        };

        _slotRepo.Setup(r => r.ListByRangeAsync(
                futureDateVn, futureDateVn, _doctorId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        var (successCount, errorCount) = await _sut.CreateOvertimeSlotsAsync(request, _doctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(6, successCount);
        Assert.Equal(0, errorCount);
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    #endregion

    #region Helper Methods

    private void SetupDoctor()
    {
        _userRepo.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                UserId = _doctorId,
                FullName = "Dr. Test",
                Role = UserRole.Doctor,
                Status = UserStatus.Active,
            });
    }

    private ScheduleSlot CreateSlot(DateOnly slotDate, SlotStatus status)
    {
        return new ScheduleSlot
        {
            SlotId = _slotId,
            DoctorId = _doctorId,
            SlotDate = slotDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Appointments = new List<Appointment>(),
        };
    }

    private static DateOnly GetNextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7; // If today is Monday, get next Monday
        return today.AddDays(daysUntilMonday);
    }

    #endregion
}
