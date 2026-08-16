using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

public class CreateOvertimeSlotsAsyncTests
{
    private readonly Mock<IScheduleSlotRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IValidator<CreateScheduleSlotRequest>> _validator = new();
    private readonly ScheduleSlotService _sut;

    public CreateOvertimeSlotsAsyncTests()
    {
        _sut = new ScheduleSlotService(_repo.Object, _userRepo.Object, _validator.Object);
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_EmptyDoctorId_Throws()
    {
        var request = new CreateOvertimeSlotsRequest { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateOvertimeSlotsAsync(request, Guid.Empty));
        
        Assert.Equal("doctorId is required.", ex.Message);
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_DoctorNotFound_Throws()
    {
        var doctorId = Guid.NewGuid();
        var request = new CreateOvertimeSlotsRequest { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow) };

        _userRepo.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateOvertimeSlotsAsync(request, doctorId));
        
        Assert.Equal($"User '{doctorId}' is not a valid Doctor.", ex.Message);
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_UserIsNotDoctor_Throws()
    {
        var doctorId = Guid.NewGuid();
        var request = new CreateOvertimeSlotsRequest { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var patientUser = new User { UserId = doctorId, Role = UserRole.Patient };

        _userRepo.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(patientUser);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateOvertimeSlotsAsync(request, doctorId));
        
        Assert.Equal($"User '{doctorId}' is not a valid Doctor.", ex.Message);
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_FutureDate_NoOverlaps_Creates6Slots()
    {
        var doctorId = Guid.NewGuid();
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)); // Tomorrow (always future)
        var request = new CreateOvertimeSlotsRequest { VisitDate = visitDate };
        var doctorUser = new User { UserId = doctorId, Role = UserRole.Doctor };

        _userRepo.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(doctorUser);
        _repo.Setup(r => r.ListByRangeAsync(visitDate, visitDate, doctorId, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ScheduleSlot>()); // No existing slots

        var (successCount, errorCount) = await _sut.CreateOvertimeSlotsAsync(request, doctorId);

        Assert.Equal(6, successCount);
        Assert.Equal(0, errorCount);
        _repo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_PastDate_Creates0Slots()
    {
        var doctorId = Guid.NewGuid();
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)); // Yesterday (always past)
        var request = new CreateOvertimeSlotsRequest { VisitDate = visitDate };
        var doctorUser = new User { UserId = doctorId, Role = UserRole.Doctor };

        _userRepo.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(doctorUser);
        _repo.Setup(r => r.ListByRangeAsync(visitDate, visitDate, doctorId, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ScheduleSlot>());

        var (successCount, errorCount) = await _sut.CreateOvertimeSlotsAsync(request, doctorId);

        Assert.Equal(0, successCount);
        Assert.Equal(6, errorCount); // All 6 failed due to being in the past
        _repo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOvertimeSlotsAsync_WithOverlaps_SkipsOverlappingSlots()
    {
        var doctorId = Guid.NewGuid();
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)); // Tomorrow
        var request = new CreateOvertimeSlotsRequest { VisitDate = visitDate };
        var doctorUser = new User { UserId = doctorId, Role = UserRole.Doctor };

        var existingSlot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctorId,
            SlotDate = visitDate,
            StartTime = new TimeOnly(17, 15),
            EndTime = new TimeOnly(17, 45) // Overlaps 17:00-17:30 and 17:30-18:00
        };

        _userRepo.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(doctorUser);
        _repo.Setup(r => r.ListByRangeAsync(visitDate, visitDate, doctorId, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ScheduleSlot> { existingSlot });

        var (successCount, errorCount) = await _sut.CreateOvertimeSlotsAsync(request, doctorId);

        // First 2 slots should fail overlap test. Remaining 4 should succeed.
        Assert.Equal(4, successCount);
        Assert.Equal(2, errorCount);
        _repo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }
}
