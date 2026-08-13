using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;
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
    private readonly IValidator<CreateScheduleSlotRequest> _validator;
    private readonly ScheduleSlotService _sut;

    // Test data
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _slotId = Guid.NewGuid();

    public ScheduleSlotServiceTests()
    {
        _validator = new CreateScheduleSlotRequestValidator();
        _sut = new ScheduleSlotService(
            _slotRepo.Object,
            _userRepo.Object,
            _validator);

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
        var (items, totalCount) = await _sut.ListSlotsAsync(doctorId: _doctorId);

        // Assert
        Assert.NotNull(items);
        Assert.True(totalCount >= 0);
    }

    [Fact]
    public async Task ListSlotsAsync_EmptyDoctorId_ThrowsException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ListSlotsAsync(doctorId: Guid.Empty));

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
                doctorId: _doctorId));

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
        await _sut.EnsureUpcomingSlotsAsync(_doctorId);

        // Assert
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureUpcomingSlotsAsync_EmptyDoctorId_ThrowsException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.EnsureUpcomingSlotsAsync(Guid.Empty));

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
            () => _sut.EnsureUpcomingSlotsAsync(_patientId));

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
        var result = await _sut.GetSlotAsync(_slotId);

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
        var result = await _sut.GetSlotAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateSlotAsync Tests

    [Fact]
    public async Task CreateSlotAsync_ValidRequest_CreatesSlot()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        _slotRepo.Setup(r => r.HasOverlapAsync(
                _doctorId, futureDate, request.StartTime, request.EndTime,
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        var result = await _sut.CreateSlotAsync(_doctorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(futureDate, result.SlotDate);
        Assert.Equal(SlotStatus.Open, result.Status);
    }

    [Fact]
    public async Task CreateSlotAsync_InvalidRequest_ThrowsValidationException()
    {
        // Arrange - request with StartTime >= EndTime
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(9, 0), // End before Start
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.CreateSlotAsync(_doctorId, request));
    }

    [Fact]
    public async Task CreateSlotAsync_DurationTooShort_ThrowsValidationException()
    {
        // Arrange - duration less than 15 minutes
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 10), // Only 10 minutes
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.CreateSlotAsync(_doctorId, request));
    }

    [Fact]
    public async Task CreateSlotAsync_NotADoctor_ThrowsException()
    {
        // Arrange
        _userRepo.Setup(r => r.GetByIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                UserId = _patientId,
                FullName = "Patient User",
                Role = UserRole.Patient,
                Status = UserStatus.Active,
            });

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateSlotAsync(_patientId, request));

        Assert.Contains("not a valid Doctor", ex.Message);
    }

    [Fact]
    public async Task CreateSlotAsync_OverlappingSlot_ThrowsException()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        _slotRepo.Setup(r => r.HasOverlapAsync(
                _doctorId, futureDate, request.StartTime, request.EndTime,
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Overlap exists

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateSlotAsync(_doctorId, request));

        Assert.Contains("overlaps", ex.Message);
    }

    #endregion

    #region UpdateSlotAsync Tests

    [Fact]
    public async Task UpdateSlotAsync_ValidUpdate_UpdatesSlot()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _slotRepo.Setup(r => r.HasOverlapAsync(
                slot.DoctorId, slot.SlotDate,
                new TimeOnly(10, 0), new TimeOnly(11, 0),
                _slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        var request = new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
        };

        // Act
        var result = await _sut.UpdateSlotAsync(_slotId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new TimeOnly(10, 0), result.StartTime);
        Assert.Equal(new TimeOnly(11, 0), result.EndTime);
    }

    [Fact]
    public async Task UpdateSlotAsync_ClosedSlot_ThrowsException()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Closed); // Already closed

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        var request = new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateSlotAsync(_slotId, request));

        Assert.Contains("Cannot update a closed slot", ex.Message);
    }

    [Fact]
    public async Task UpdateSlotAsync_SlotNotFound_ThrowsException()
    {
        // Arrange
        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot?)null);

        var request = new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateSlotAsync(Guid.NewGuid(), request));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task UpdateSlotAsync_UpdatedOverlap_ThrowsException()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, SlotStatus.Open);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(_slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        _slotRepo.Setup(r => r.HasOverlapAsync(
                slot.DoctorId, slot.SlotDate,
                new TimeOnly(10, 0), new TimeOnly(11, 0),
                _slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Overlap with new time

        var request = new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateSlotAsync(_slotId, request));

        Assert.Contains("overlaps", ex.Message);
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
        var result = await _sut.CloseSlotAsync(_slotId, forceClose: false);

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
        var result = await _sut.CloseSlotAsync(_slotId, forceClose: false);

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
        var result = await _sut.CloseSlotAsync(_slotId, forceClose: true);

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
            () => _sut.CloseSlotAsync(_slotId, forceClose: false));

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
        var result = await _sut.ReopenSlotAsync(_slotId);

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
            () => _sut.ReopenSlotAsync(_slotId));

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
            () => _sut.ReopenSlotAsync(Guid.NewGuid()));

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
        await _sut.EnsureDefaultSlotsAsync(_doctorId, weekStart);

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
            () => _sut.EnsureDefaultSlotsAsync(_doctorId, notMonday));

        Assert.Contains("weekStart must be a Monday", ex.Message);
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
