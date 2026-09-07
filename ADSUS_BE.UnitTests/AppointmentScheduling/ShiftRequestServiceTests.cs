using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

public class ShiftRequestServiceTests
{
    private readonly Mock<IShiftRequestRepository> _repoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly AppDbContext _db;
    private readonly ShiftRequestService _sut;
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public ShiftRequestServiceTests()
    {
        _repoMock = new Mock<IShiftRequestRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _notificationMock = new Mock<INotificationService>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _db.Users.Add(new User 
        { 
            UserId = _adminId, 
            Role = UserRole.Admin, 
            FullName = "Admin Test",
            PasswordHash = "hashed",
            Phone = "0123456789"
        });
        _db.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ScheduleSettings:MinAdvanceDaysForLeave", "2" }
            })
            .Build();

        _sut = new ShiftRequestService(
            _repoMock.Object,
            _userRepoMock.Object,
            _db,
            _notificationMock.Object,
            config);

        _userRepoMock.Setup(u => u.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = _doctorId, Role = UserRole.Doctor, FullName = "Dr. Test" });
    }

    [Fact]
    public async Task CreateRequestAsync_ValidLeave_ReturnsCreated()
    {
        var dto = new CreateShiftRequestDto
        {
            RequestType = ShiftRequestType.Leave,
            ShiftType = ShiftType.Morning,
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Reason = "Test reason"
        };

        _repoMock.Setup(r => r.HasActiveRequestAsync(_doctorId, dto.RequestDate, dto.ShiftType, dto.RequestType, default))
            .ReturnsAsync(false);

        var result = await _sut.CreateRequestAsync(_doctorId, dto);

        Assert.NotNull(result);
        Assert.Equal(ShiftRequestStatus.Pending, result.Status);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<ShiftRequest>(), default), Times.Once);
        // Kiểm tra đã gửi notification cho Admin
        _notificationMock.Verify(n => n.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == _adminId && r.Type == "shift_request_new"), default), Times.Once);
    }

    [Fact]
    public async Task CreateRequestAsync_DuplicateRequest_ThrowsException()
    {
        var dto = new CreateShiftRequestDto
        {
            RequestType = ShiftRequestType.Leave,
            ShiftType = ShiftType.FullDay,
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
        };

        _repoMock.Setup(r => r.HasActiveRequestAsync(_doctorId, dto.RequestDate, dto.ShiftType, dto.RequestType, default))
            .ReturnsAsync(true); // Trả về true (đã có request overlap)

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateRequestAsync(_doctorId, dto));
        Assert.Contains("Bạn đã có yêu cầu cho Ca Sáng hoặc Ca Chiều", ex.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_LessThanMinDays_ThrowsException()
    {
        var dto = new CreateShiftRequestDto
        {
            RequestType = ShiftRequestType.Leave,
            ShiftType = ShiftType.Morning,
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Reason = "Too soon"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateRequestAsync(_doctorId, dto));
        Assert.Contains("trước ít nhất 2 ngày", ex.Message);
    }

    [Fact]
    public async Task ReviewRequestAsync_ApproveLeave_ClosesSlotsAndCancelsAppointments()
    {
        var requestId = Guid.NewGuid();
        var requestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        
        var request = new ShiftRequest
        {
            RequestId = requestId,
            UserId = _doctorId,
            RequestType = ShiftRequestType.Leave,
            ShiftType = ShiftType.Morning,
            RequestDate = requestDate,
            Status = ShiftRequestStatus.Pending,
            User = new User { UserId = _doctorId, FullName = "Dr. Test" }
        };

        _repoMock.Setup(r => r.GetByIdAsync(requestId, default))
            .ReturnsAsync(request);

        // Add slots to DB
        var patientId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();
        _db.PatientProfiles.Add(new PatientProfile { PatientProfileId = patientId, UserId = patientUserId });
        
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctorId,
            SlotDate = requestDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(8, 30),
            Status = SlotStatus.Open
        };
        _db.ScheduleSlots.Add(slot);
        
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = patientId,
            Status = AppointmentStatus.Booked
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        var dto = new ReviewShiftRequestDto { Decision = "APPROVED" };

        var result = await _sut.ReviewRequestAsync(requestId, _adminId, dto);

        Assert.Equal(ShiftRequestStatus.Approved, result.Status);
        
        var updatedSlot = await _db.ScheduleSlots.FirstAsync(s => s.SlotId == slot.SlotId);
        Assert.Equal(SlotStatus.Closed, updatedSlot.Status);

        var updatedAppointment = await _db.Appointments.FirstAsync(a => a.AppointmentId == appointment.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, updatedAppointment.Status);

        // Gửi noti cho bệnh nhân bị hủy
        _notificationMock.Verify(n => n.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == patientUserId && r.Type == "appointment_cancellation"), default), Times.Once);
        
        // Gửi noti cho bác sĩ
        _notificationMock.Verify(n => n.SendAsync(It.Is<SendNotificationRequest>(r => r.UserId == _doctorId && r.Type == "shift_request_reviewed"), default), Times.Once);
    }

    [Fact]
    public async Task ReviewRequestAsync_RejectWithoutReason_ThrowsException()
    {
        var requestId = Guid.NewGuid();
        var request = new ShiftRequest { RequestId = requestId, Status = ShiftRequestStatus.Pending };
        _repoMock.Setup(r => r.GetByIdAsync(requestId, default)).ReturnsAsync(request);

        var dto = new ReviewShiftRequestDto { Decision = "REJECTED", RejectReason = "" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReviewRequestAsync(requestId, _adminId, dto));
        Assert.Contains("lý do từ chối", ex.Message);
    }
}
