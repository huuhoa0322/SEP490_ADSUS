using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Bộ kiểm thử toàn diện chéo theo implementation_plan.md cho Backend & Business Logic:
/// - TC-01 (Dynamic Slot Recycling): Slot có ca khám xong sớm trước giờ hẹn, khi gọi RecycleCompletedEarlySlotsAsync
///   (hoặc ReadyForNextPatientAsync), slot đang Booked được mở lại thành Open.
/// - TC-02 (Booking on Recycled Slot): Slot vừa được mở lại (Open), bệnh nhân mới đặt lịch vào slot này thành công 100%,
///   không bị lỗi slot bận.
/// - TC-03 (Slot Busy with InProgress Case): Slot có ca khám đang InProgress -> ListOpenSlotsAsync, BookAppointmentAsync,
///   CreateFollowUpAppointmentAsync, RescheduleAppointmentAsync đều nhận diện là BẬN và chặn không cho đặt đè.
/// - TC-04 (Walk-in 5-Minute Rule):
///   * Staff đặt walk-in tại phút thứ 3 khi bác sĩ rảnh -> Thành công.
///   * Staff đặt walk-in tại phút thứ 7 -> Quăng ngoại lệ quá 5 phút.
///   * Staff đặt walk-in khi bác sĩ đang có ca InProgress -> Quăng ngoại lệ bác sĩ đang bận.
/// - TC-05 (Staff Bypass Spam Limits):
///   * Staff đặt lịch thứ 4, thứ 5 cho bệnh nhân -> Thành công (bỏ qua Pool 1, 2, 3 và Same-Day limit).
/// - TC-06 (Staff Proxy Booking for Relative):
///   * Staff đặt lịch kèm RelationshipId của con gái Người mẹ -> appointment.BookedByUserId được gán chính xác bằng
///     MotherUserId, và notification gửi tới MotherUserId.
/// - TC-07 (No-Show Slot Release):
///   * Quá 15 phút không check-in, gọi ProcessNoShowAsync -> appointment.Status = NoShow, slot.Status = Open.
/// - TC-08 (Security & Medical Characters):
///   * HtmlHelper.StripToPlainText("<script>alert('xss')</script>Bình thường") -> "Bình thường".
///   * HtmlHelper.StripToPlainText("Bạch cầu < 4.0 và SpO2 > 95%") -> Giữ nguyên 100% "< 4.0" và "> 95%".
/// - TC-09 (CreateFromBookingAsync Null Guard):
///   * Gọi CreateFromBookingAsync với symptoms = null -> Tạo case bình thường với CaseSymptoms rỗng, không quăng exception.
/// </summary>
public class DynamicSlotRecyclingAndStaffProxyComprehensiveTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICaseService> _caseService = new();
    private readonly NoShowService _noShowService;
    private readonly AppointmentService _appointmentService;

    // Default IDs
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _staffUserId = Guid.NewGuid();
    private readonly Guid _staffProfileId = Guid.NewGuid();
    private readonly Guid _patientUserId = Guid.NewGuid();
    private readonly Guid _patientProfileId = Guid.NewGuid();

    private static readonly TimeZoneInfo VietnamZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public DynamicSlotRecyclingAndStaffProxyComprehensiveTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        // Setup Doctor User
        var doctor = new User
        {
            UserId = _doctorId,
            FullName = "BS. Nguyễn Văn A",
            Phone = "0988111222",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            Gender = GenderType.Male,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(doctor);

        // Setup Staff User
        var staff = new User
        {
            UserId = _staffUserId,
            FullName = "Lễ tân Trần Thị B",
            Phone = "0977222333",
            Role = UserRole.Staff,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var staffProfile = new PatientProfile
        {
            PatientProfileId = _staffProfileId,
            UserId = _staffUserId,
            FullName = "Lễ tân Trần Thị B",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(staff);
        _db.PatientProfiles.Add(staffProfile);

        // Setup Patient User & Profile
        var patient = new User
        {
            UserId = _patientUserId,
            FullName = "Bệnh nhân Lê Văn C",
            Phone = "0966333444",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var patientProfile = new PatientProfile
        {
            PatientProfileId = _patientProfileId,
            UserId = _patientUserId,
            FullName = "Bệnh nhân Lê Văn C",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(patient);
        _db.PatientProfiles.Add(patientProfile);

        _db.SaveChanges();

        // Repository setups
        _profileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.PatientProfiles.Find(id));

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Callback<Appointment, CancellationToken>((appt, _) =>
            {
                _db.Appointments.Add(appt);
                _db.SaveChanges();
            })
            .ReturnsAsync((Appointment appt, CancellationToken _) => appt);

        _appointmentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.Appointments
                .Include(a => a.Slot).ThenInclude(s => s.Doctor)
                .Include(a => a.PatientProfile)
                .FirstOrDefault(a => a.AppointmentId == id));

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Callback<ScheduleSlot, CancellationToken>((s, _) => _db.SaveChanges())
            .Returns(Task.CompletedTask);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.ScheduleSlots
                .Include(s => s.Doctor)
                .Include(s => s.Appointments).ThenInclude(a => a.Case)
                .FirstOrDefault(s => s.SlotId == id));

        _slotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.ScheduleSlots
                .Include(s => s.Doctor)
                .Include(s => s.Appointments).ThenInclude(a => a.Case)
                .FirstOrDefault(s => s.SlotId == id));

        _slotRepo.Setup(r => r.ListByRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly from, DateOnly to, Guid? docId, SlotStatus? status, CancellationToken _) =>
            {
                var q = _db.ScheduleSlots
                    .Include(s => s.Doctor)
                    .Include(s => s.Appointments).ThenInclude(a => a.Case)
                    .Where(s => s.SlotDate >= from && s.SlotDate <= to);

                if (docId.HasValue) q = q.Where(s => s.DoctorId == docId.Value);
                if (status.HasValue) q = q.Where(s => s.Status == status.Value);

                return q.ToList();
            });

        _caseService.Setup(c => c.CreateFromBookingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<SymptomInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _notificationService.Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var noShowSettings = Options.Create(new NoShowSettings { GraceTimeMinutes = 15 });
        _noShowService = new NoShowService(
            _db,
            noShowSettings,
            _notificationService.Object,
            _profileRepo.Object,
            Mock.Of<ILogger<NoShowService>>());

        _appointmentService = new AppointmentService(
            _appointmentRepo.BackedBy(_db).Object,
            _slotRepo.BackedBy(_db).Object,
            new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db),
            new ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService(_profileRepo.Object, new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db), Microsoft.Extensions.Logging.Abstractions.NullLogger<ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService>.Instance),
            PatientAccountTestServices.Relationship(_db),
            _notificationService.Object,
            _caseService.BackedBy(_db).Object,
            _noShowService,
            _db,
            Mock.Of<ILogger<AppointmentService>>());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Time & Slot Methods

    private static (DateOnly TodayVn, TimeOnly CurrentTimeVn) GetNowVn()
    {
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
        return (DateOnly.FromDateTime(nowVn), TimeOnly.FromDateTime(nowVn));
    }

    private static (DateOnly TodayVn, TimeOnly StartTime, TimeOnly EndTime) GetFutureSlotToday()
    {
        var (todayVn, currentTimeVn) = GetNowVn();
        TimeOnly startTime;
        if (currentTimeVn < new TimeOnly(23, 40))
        {
            startTime = currentTimeVn.AddMinutes(5);
        }
        else if (currentTimeVn < new TimeOnly(23, 59, 40))
        {
            startTime = currentTimeVn.Add(TimeSpan.FromSeconds(10));
        }
        else
        {
            startTime = new TimeOnly(23, 59, 59);
        }
        var endTime = startTime.AddMinutes(20);
        return (todayVn, startTime, endTime);
    }

    private static (DateOnly TodayVn, TimeOnly ThreeMinsAgo, TimeOnly SevenMinsAgo) GetPastSlotTimesToday()
    {
        var (todayVn, currentTimeVn) = GetNowVn();
        var threeMinsAgo = currentTimeVn > new TimeOnly(0, 5) ? currentTimeVn.AddMinutes(-3) : new TimeOnly(0, 1);
        var sevenMinsAgo = currentTimeVn > new TimeOnly(0, 10) ? currentTimeVn.AddMinutes(-7) : new TimeOnly(0, 0);
        return (todayVn, threeMinsAgo, sevenMinsAgo);
    }

    private ScheduleSlot CreateSlot(DateOnly date, TimeOnly startTime, TimeOnly endTime, SlotStatus status = SlotStatus.Open, Guid? doctorId = null)
    {
        var docId = doctorId ?? _doctorId;
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = docId,
            SlotDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Doctor = _db.Users.Find(docId)!,
            Appointments = new List<Appointment>()
        };
        _db.ScheduleSlots.Add(slot);
        _db.SaveChanges();
        return slot;
    }

    #endregion

    #region TC-01: Dynamic Slot Recycling

    [Fact]
    public async Task TC01_DynamicSlotRecycling_SlotCompletedEarly_StatusTransitionsToOpen()
    {
        // Arrange
        var (todayVn, startTime, endTime) = GetFutureSlotToday();
        var slot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Booked);

        // Appointment in slot was completed early before slot end time
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = _patientProfileId,
                Status = CaseStatus.End,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        slot.Appointments.Add(appointment);
        _db.Appointments.Add(appointment);
        _db.SaveChanges();

        // Act: Doctor or system triggers RecycleCompletedEarlySlotsAsync
        await _appointmentService.RecycleCompletedEarlySlotsAsync(_doctorId, TestContext.Current.CancellationToken);

        // Assert: Slot status transitioned to Open
        var updatedSlot = await _db.ScheduleSlots.FindAsync(new object[] { slot.SlotId }, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedSlot);
        Assert.Equal(SlotStatus.Open, updatedSlot.Status);
    }

    [Fact]
    public async Task TC01_ReadyForNextPatientAsync_RecyclesEarlySlots_AndSendsNotificationToStaff()
    {
        // Arrange
        var (todayVn, startTime, endTime) = GetFutureSlotToday();
        var slot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Booked);

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        slot.Appointments.Add(appointment);
        _db.Appointments.Add(appointment);
        _db.SaveChanges();

        // Act: Doctor presses "Ready for next patient"
        await _appointmentService.ReadyForNextPatientAsync(_doctorId, TestContext.Current.CancellationToken);

        // Assert: Slot recycled to Open
        var updatedSlot = await _db.ScheduleSlots.FindAsync(new object[] { slot.SlotId }, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedSlot);
        Assert.Equal(SlotStatus.Open, updatedSlot.Status);

        // Assert: Notification sent to staff
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r =>
                r.UserId == _staffUserId &&
                r.Type == "doctor_ready_next"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TC01_RecycleCompletedEarlySlots_SlotHasActiveBookedOrInProgressCase_DoesNotRecycle()
    {
        // Arrange
        var (todayVn, startTime, endTime) = GetFutureSlotToday();

        // Slot 1: Still has active Booked appointment
        var slot1 = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Booked);
        var activeAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot1.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        slot1.Appointments.Add(activeAppt);
        _db.Appointments.Add(activeAppt);

        // Slot 2: Has completed appointment but Case is still InProgress
        var slot2 = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Booked);
        var inProgressAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot2.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = _patientProfileId,
                Status = CaseStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        slot2.Appointments.Add(inProgressAppt);
        _db.Appointments.Add(inProgressAppt);
        _db.SaveChanges();

        // Act
        await _appointmentService.RecycleCompletedEarlySlotsAsync(_doctorId, TestContext.Current.CancellationToken);

        // Assert: Neither slot is recycled to Open
        var updatedSlot1 = await _db.ScheduleSlots.FindAsync(new object[] { slot1.SlotId }, TestContext.Current.CancellationToken);
        var updatedSlot2 = await _db.ScheduleSlots.FindAsync(new object[] { slot2.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, updatedSlot1!.Status);
        Assert.Equal(SlotStatus.Booked, updatedSlot2!.Status);
    }

    #endregion

    #region TC-02: Booking on Recycled Slot

    [Fact]
    public async Task TC02_BookingOnRecycledSlot_NewPatientBooksSuccessfully_NoConflictError()
    {
        // Arrange: Slot was recycled back to Open (it still has historical Completed appointment)
        var (todayVn, startTime, endTime) = GetFutureSlotToday();
        var slot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Open);

        var oldCompletedAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = Guid.NewGuid(), // Previous patient
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-20),
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = Guid.NewGuid(),
                Status = CaseStatus.End,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-20)
            }
        };
        slot.Appointments.Add(oldCompletedAppt);
        _db.Appointments.Add(oldCompletedAppt);
        _db.SaveChanges();

        // Act: New patient books this recycled slot
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            Reason = "Khám lại sau khi slot mở"
        };

        var response = await _appointmentService.BookAppointmentAsync(_patientUserId, _patientProfileId, request, ct: TestContext.Current.CancellationToken);

        // Assert: Booking succeeds 100%, status is Booked, slot status updated to Booked
        Assert.NotNull(response);
        Assert.Equal(AppointmentStatus.Booked, response.Status);
        Assert.Equal(slot.SlotId, response.ScheduleSlotId);

        var updatedSlot = await _db.ScheduleSlots.FindAsync(new object[] { slot.SlotId }, TestContext.Current.CancellationToken);
        Assert.Equal(SlotStatus.Booked, updatedSlot!.Status);
    }

    #endregion

    #region TC-03: Slot Busy with InProgress Case

    [Fact]
    public async Task TC03_SlotWithInProgressCase_ListOpenSlotsAsync_ExcludesBusySlot()
    {
        // Arrange: Slot has status = Open, but contains an appointment whose Case is InProgress
        var (todayVn, startTime, endTime) = GetFutureSlotToday();
        var slot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Open);

        var apptWithInProgressCase = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = _patientProfileId,
                Status = CaseStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        slot.Appointments.Add(apptWithInProgressCase);
        _db.Appointments.Add(apptWithInProgressCase);
        _db.SaveChanges();

        // Act: List open slots for today
        var openSlots = await _appointmentService.ListOpenSlotsAsync(
            doctorId: _doctorId.ToString(),
            fromDate: todayVn,
            toDate: todayVn,
            ct: TestContext.Current.CancellationToken);

        // Assert: The slot is NOT in the open slots list because Case is InProgress
        Assert.DoesNotContain(openSlots, s => s.SlotId == slot.SlotId);
    }

    [Fact]
    public async Task TC03_SlotWithInProgressCase_BookAppointmentAsync_ThrowsInvalidOperationException()
    {
        // Arrange
        var (todayVn, startTime, endTime) = GetFutureSlotToday();
        var slot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Open);

        var inProgressAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = Guid.NewGuid(),
            Status = AppointmentStatus.Completed,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = Guid.NewGuid(),
                Status = CaseStatus.InProgress
            }
        };
        slot.Appointments.Add(inProgressAppt);
        _db.Appointments.Add(inProgressAppt);
        _db.SaveChanges();

        // Act & Assert
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            Reason = "Đặt lịch đè"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_patientUserId, _patientProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Slot này đã có người đặt hoặc đang có ca khám.", ex.Message);
    }

    [Fact]
    public async Task TC03_SlotWithInProgressCase_CreateFollowUpAppointmentAsync_ThrowsInvalidOperationException()
    {
        // Arrange
        var (todayVn, startTime, endTime) = GetFutureSlotToday();
        var slot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Open);

        var inProgressAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = Guid.NewGuid(),
            Status = AppointmentStatus.Completed,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = Guid.NewGuid(),
                Status = CaseStatus.InProgress
            }
        };
        slot.Appointments.Add(inProgressAppt);
        _db.Appointments.Add(inProgressAppt);
        _db.SaveChanges();

        // Act & Assert: Doctor creates follow-up on this slot
        var request = new FollowUpAppointmentRequest
        {
            PatientProfileId = _patientProfileId,
            ScheduleSlotId = slot.SlotId,
            Reason = "Tái khám sau 1 tuần"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.CreateFollowUpAppointmentAsync(_doctorId, request, TestContext.Current.CancellationToken));

        Assert.Contains("Khung giờ này hiện không khả dụng để đặt tái khám.", ex.Message);
    }

    [Fact]
    public async Task TC03_SlotWithInProgressCase_RescheduleAppointmentAsync_ThrowsInvalidOperationException()
    {
        // Arrange
        var (todayVn, startTime, endTime) = GetFutureSlotToday();

        // Old appointment to reschedule
        var oldSlot = CreateSlot(todayVn.AddDays(2), new TimeOnly(10, 0), new TimeOnly(10, 30), status: SlotStatus.Booked);
        var oldAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = oldSlot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        oldSlot.Appointments.Add(oldAppt);
        _db.Appointments.Add(oldAppt);

        // New slot has Open status but its Case is InProgress
        var targetSlot = CreateSlot(todayVn, startTime, endTime, status: SlotStatus.Open);
        var inProgressAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = targetSlot.SlotId,
            PatientProfileId = Guid.NewGuid(),
            Status = AppointmentStatus.Completed,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = Guid.NewGuid(),
                Status = CaseStatus.InProgress
            }
        };
        targetSlot.Appointments.Add(inProgressAppt);
        _db.Appointments.Add(inProgressAppt);
        _db.SaveChanges();

        // Act & Assert
        var request = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = targetSlot.SlotId,
            RescheduleReason = "Đổi sang ca sớm hơn"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.RescheduleAppointmentAsync(oldAppt.AppointmentId, request, TestContext.Current.CancellationToken));

        Assert.Contains("Khung giờ mới này đã có người đặt hoặc đang có ca khám.", ex.Message);
    }

    #endregion

    #region TC-04: Walk-in 5-Minute Rule

    [Fact]
    public async Task TC04_WalkIn_AtMinute3_DoctorFree_StaffBookingSucceeds()
    {
        // Arrange: Slot started 3 minutes ago
        var (todayVn, threeMinsAgo, _) = GetPastSlotTimesToday();
        var slot = CreateSlot(todayVn, threeMinsAgo, threeMinsAgo.AddMinutes(30), status: SlotStatus.Open);

        // Doctor has no InProgress case in _db.Cases
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            Reason = "Khách vãng lai tại quầy (3 phút)"
        };

        // Act: Staff books with isStaffOverride = true
        var response = await _appointmentService.BookAppointmentAsync(
            _staffUserId, _patientProfileId, request, isStaffOverride: true, ct: TestContext.Current.CancellationToken);

        // Assert: Booking succeeds
        Assert.NotNull(response);
        Assert.Equal(AppointmentStatus.Booked, response.Status);
        Assert.Equal(slot.SlotId, response.ScheduleSlotId);
    }

    [Fact]
    public async Task TC04_WalkIn_AtMinute7_Exceeds5Minutes_ThrowsInvalidOperationException()
    {
        // Arrange: Slot started 7 minutes ago
        var (todayVn, _, sevenMinsAgo) = GetPastSlotTimesToday();
        var slot = CreateSlot(todayVn, sevenMinsAgo, sevenMinsAgo.AddMinutes(30), status: SlotStatus.Open);

        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            Reason = "Khách vãng lai đến trễ (7 phút)"
        };

        // Act & Assert: Staff books past 5 minutes -> Throws exception
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(
                _staffUserId, _patientProfileId, request, isStaffOverride: true, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Đã quá 5 phút kể từ đầu ca. Vui lòng đặt vào slot tiếp theo rồi đẩy khám sớm.", ex.Message);
    }

    [Fact]
    public async Task TC04_WalkIn_AtMinute3_SlotHasInProgressCase_ThrowsInvalidOperationException()
    {
        // Arrange: Slot started 3 minutes ago, but this slot has an active InProgress case
        var (todayVn, threeMinsAgo, _) = GetPastSlotTimesToday();
        var slot = CreateSlot(todayVn, threeMinsAgo, threeMinsAgo.AddMinutes(30), status: SlotStatus.Open);

        // Slot has an appointment with active InProgress case
        var apptWithInProgressCase = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = Guid.NewGuid(),
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Case = new Case
            {
                CaseId = Guid.NewGuid(),
                DoctorId = _doctorId,
                PatientProfileId = Guid.NewGuid(),
                Status = CaseStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        slot.Appointments.Add(apptWithInProgressCase);
        _db.Appointments.Add(apptWithInProgressCase);
        _db.SaveChanges();

        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            Reason = "Khách vãng lai khi slot đang khám"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(
                _staffUserId, _patientProfileId, request, isStaffOverride: true, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Slot này đã có người đặt hoặc đang có ca khám.", ex.Message);
    }

    [Fact]
    public async Task TC04_WalkIn_PatientBookingPastSlot_WithoutStaffOverride_ThrowsInvalidOperationException()
    {
        // Arrange: Patient (online) tries to book a past slot today
        var (todayVn, threeMinsAgo, _) = GetPastSlotTimesToday();
        var slot = CreateSlot(todayVn, threeMinsAgo, threeMinsAgo.AddMinutes(30), status: SlotStatus.Open);

        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            Reason = "Bệnh nhân cố tình đặt slot đã qua"
        };

        // Act & Assert: isStaffOverride = false -> Throws
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(
                _patientUserId, _patientProfileId, request, isStaffOverride: false, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Không thể đặt lịch vào khung giờ đã qua.", ex.Message);
    }

    #endregion

    #region TC-05: Staff Bypass Spam Limits

    [Fact]
    public async Task TC05_StaffBypass_PatientHas3ActiveBookings_StaffCanBookFourthAndFifth()
    {
        // Arrange: Patient already has 3 active (Booked) appointments in the future
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        for (int i = 0; i < 3; i++)
        {
            var existingSlot = CreateSlot(futureDate.AddDays(i), new TimeOnly(8, 0), new TimeOnly(8, 30), status: SlotStatus.Booked);
            var appt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = existingSlot.SlotId,
                PatientProfileId = _patientProfileId,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            existingSlot.Appointments.Add(appt);
            _db.Appointments.Add(appt);
        }
        _db.SaveChanges();

        // Slots for 4th and 5th appointments
        var slot4 = CreateSlot(futureDate.AddDays(4), new TimeOnly(9, 0), new TimeOnly(9, 30), status: SlotStatus.Open);
        var slot5 = CreateSlot(futureDate.AddDays(5), new TimeOnly(9, 0), new TimeOnly(9, 30), status: SlotStatus.Open);

        // Verify patient herself would be blocked by Pool 1 & Pool 3 limits
        var patientReq4 = new BookAppointmentRequest { ScheduleSlotId = slot4.SlotId, Reason = "Bệnh nhân tự đặt lần 4" };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_patientUserId, _patientProfileId, patientReq4, isStaffOverride: false, ct: TestContext.Current.CancellationToken));
        Assert.Contains("lịch hẹn đang chờ", ex.Message);

        // Act: Staff books the 4th appointment
        var staffReq4 = new BookAppointmentRequest { ScheduleSlotId = slot4.SlotId, Reason = "Staff hỗ trợ đặt lần 4" };
        var res4 = await _appointmentService.BookAppointmentAsync(_staffUserId, _patientProfileId, staffReq4, isStaffOverride: true, ct: TestContext.Current.CancellationToken);

        // Act: Staff books the 5th appointment
        var staffReq5 = new BookAppointmentRequest { ScheduleSlotId = slot5.SlotId, Reason = "Staff hỗ trợ đặt lần 5" };
        var res5 = await _appointmentService.BookAppointmentAsync(_staffUserId, _patientProfileId, staffReq5, isStaffOverride: true, ct: TestContext.Current.CancellationToken);

        // Assert: Both 4th and 5th bookings succeed!
        Assert.NotNull(res4);
        Assert.Equal(AppointmentStatus.Booked, res4.Status);
        Assert.NotNull(res5);
        Assert.Equal(AppointmentStatus.Booked, res5.Status);
    }

    [Fact]
    public async Task TC05_SameDayLimit_AppliesToBothPatientAndStaff_CannotBookSecondBookedAppointmentOnSameDay()
    {
        // Arrange: Patient already has an active Booked appointment on futureDate
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var morningSlot = CreateSlot(futureDate, new TimeOnly(8, 0), new TimeOnly(8, 30), status: SlotStatus.Booked);
        var morningAppt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = morningSlot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        morningSlot.Appointments.Add(morningAppt);
        _db.Appointments.Add(morningAppt);
        _db.SaveChanges();

        // Afternoon slot on the same date
        var afternoonSlot = CreateSlot(futureDate, new TimeOnly(14, 0), new TimeOnly(14, 30), status: SlotStatus.Open);

        // 1. Verify patient herself is blocked by Same-Day limit
        var patientReq = new BookAppointmentRequest { ScheduleSlotId = afternoonSlot.SlotId, Reason = "Tự đặt ca chiều" };
        var patientEx = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_patientUserId, _patientProfileId, patientReq, isStaffOverride: false, ct: TestContext.Current.CancellationToken));
        Assert.Contains("Mỗi ngày chỉ được đặt tối đa 1 lịch", patientEx.Message);

        // 2. Verify Staff is ALSO blocked by Same-Day limit (1 patient can only have 1 Booked appointment per day)
        var staffReq = new BookAppointmentRequest { ScheduleSlotId = afternoonSlot.SlotId, Reason = "Staff đặt thêm ca chiều" };
        var staffEx = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_staffUserId, _patientProfileId, staffReq, isStaffOverride: true, ct: TestContext.Current.CancellationToken));
        Assert.Contains("Mỗi ngày chỉ được đặt tối đa 1 lịch", staffEx.Message);
    }

    #endregion

    #region TC-06: Staff Proxy Booking for Relative

    [Fact]
    public async Task TC06_StaffProxyBooking_ForRelative_AssignsBookedByUserIdToMother_AndSendsNotificationToMother()
    {
        // Arrange:
        // 1. Mother User (UserRole.Patient)
        var motherUserId = Guid.NewGuid();
        var motherUser = new User
        {
            UserId = motherUserId,
            FullName = "Nguyễn Thị Mẹ",
            Phone = "0911555666",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var motherProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = motherUserId,
            FullName = "Nguyễn Thị Mẹ",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(motherUser);
        _db.PatientProfiles.Add(motherProfile);

        // 2. Daughter Profile (No account, managed under Mother)
        var daughterProfileId = Guid.NewGuid();
        var daughterProfile = new PatientProfile
        {
            PatientProfileId = daughterProfileId,
            UserId = null,
            FullName = "Bé Nguyễn Thị Con Gái",
            DateOfBirth = new DateOnly(2020, 1, 1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(daughterProfile);

        // 3. PatientRelationship between Mother and Daughter
        var relationshipId = Guid.NewGuid();
        var relationship = new PatientRelationship
        {
            RelationshipId = relationshipId,
            UserId = motherUserId,
            PatientProfileId = daughterProfileId,
            RelationshipName = "Con gái",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(relationship);

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, new TimeOnly(10, 0), new TimeOnly(10, 30), status: SlotStatus.Open);
        _db.SaveChanges();

        // Act: Staff books appointment with RelationshipId of Daughter
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            RelationshipId = relationshipId,
            Reason = "Khám tai mũi họng cho bé"
        };

        var response = await _appointmentService.BookAppointmentAsync(
            _staffUserId, _staffProfileId, request, isStaffOverride: true, ct: TestContext.Current.CancellationToken);

        // Assert:
        Assert.NotNull(response);
        Assert.Equal(AppointmentStatus.Booked, response.Status);

        // Verify stored appointment in database
        var createdAppt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == response.AppointmentId, TestContext.Current.CancellationToken);
        Assert.NotNull(createdAppt);
        Assert.Equal(daughterProfileId, createdAppt.PatientProfileId);
        Assert.Equal(motherUserId, createdAppt.BookedByUserId); // Assigned to MotherUserId, NOT StaffUserId!
        Assert.Equal(relationshipId, createdAppt.RelationshipId);

        // Verify notification was sent to MotherUserId
        _notificationService.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(r =>
                r.UserId == motherUserId &&
                r.Type == "appointment_booking"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC06_StaffProxyBooking_NonExistentRelationship_ThrowsInvalidOperationException()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var slot = CreateSlot(futureDate, new TimeOnly(10, 0), new TimeOnly(10, 30), status: SlotStatus.Open);

        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            RelationshipId = Guid.NewGuid(), // Non-existent relationship
            Reason = "Đặt hộ mối quan hệ giả"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(
                _staffUserId, _staffProfileId, request, isStaffOverride: true, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Không tìm thấy mối quan hệ này trong danh bạ.", ex.Message);
    }

    #endregion

    #region TC-07: No-Show Slot Release

    [Fact]
    public async Task TC07_NoShow_OverGracePeriod_ReleasesSlotToOpen()
    {
        // Arrange: Appointment scheduled 20 minutes ago (exceeding 15-minute grace period)
        var targetUtc = DateTime.UtcNow.AddMinutes(-20);
        var targetLocal = targetUtc.Add(ClinicClock.Offset);
        var slotDate = DateOnly.FromDateTime(targetLocal);
        var startTime = TimeOnly.FromDateTime(targetLocal);

        var slot = CreateSlot(slotDate, startTime, startTime.AddMinutes(30), status: SlotStatus.Booked);
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = targetUtc.AddDays(-1),
            UpdatedAt = targetUtc.AddDays(-1),
            Slot = slot,
            CaseId = Guid.NewGuid()
        };
        slot.Appointments.Add(appt);
        _db.Appointments.Add(appt);

        var medicalCase = new Case
        {
            CaseId = appt.CaseId.Value,
            DoctorId = _doctorId,
            PatientProfileId = _patientProfileId,
            Status = CaseStatus.Booked,
            CreatedAt = targetUtc.AddDays(-1),
            UpdatedAt = targetUtc.AddDays(-1)
        };
        _db.Cases.Add(medicalCase);
        _db.SaveChanges();

        // Act: Process No-Show
        var result = await _noShowService.ProcessNoShowAsync(appt, TestContext.Current.CancellationToken);

        // Assert:
        Assert.True(result.WasProcessed);
        Assert.Equal(AppointmentStatus.NoShow, appt.Status);
        Assert.Equal(SlotStatus.Open, slot.Status); // Slot released back to OPEN!
        Assert.Equal(CaseStatus.Cancelled, medicalCase.Status); // Case cancelled
    }

    [Fact]
    public async Task TC07_NoShow_WithinGracePeriod_DoesNotReleaseSlot()
    {
        // Arrange: Appointment scheduled 5 minutes ago (within 15-minute grace period)
        var targetUtc = DateTime.UtcNow.AddMinutes(-5);
        var targetLocal = targetUtc.Add(ClinicClock.Offset);
        var slotDate = DateOnly.FromDateTime(targetLocal);
        var startTime = TimeOnly.FromDateTime(targetLocal);

        var slot = CreateSlot(slotDate, startTime, startTime.AddMinutes(30), status: SlotStatus.Booked);
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _patientProfileId,
            Status = AppointmentStatus.Booked,
            CreatedAt = targetUtc.AddDays(-1),
            UpdatedAt = targetUtc.AddDays(-1),
            Slot = slot
        };
        slot.Appointments.Add(appt);
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        // Act: Process No-Show
        var result = await _noShowService.ProcessNoShowAsync(appt, TestContext.Current.CancellationToken);

        // Assert: Not processed, remains Booked
        Assert.False(result.WasProcessed);
        Assert.Equal(AppointmentStatus.Booked, appt.Status);
        Assert.Equal(SlotStatus.Booked, slot.Status);
    }

    #endregion

    #region TC-08: Security & Medical Characters

    [Theory]
    [InlineData("<script>alert('xss')</script>Bình thường", "Bình thường")]
    [InlineData("<script src='hack.js'></script>   Khám tổng quát   ", "Khám tổng quát")]
    [InlineData("<img src=x onerror=alert(1)>Đau bụng âm ỉ", "Đau bụng âm ỉ")]
    [InlineData("<iframe src='evil.com'></iframe>Siêu âm định kỳ", "Siêu âm định kỳ")]
    [InlineData("<b>Đậm</b> và <i>nghiêng</i>", "Đậm và nghiêng")]
    public void TC08_HtmlHelper_StripsXssAndDangerousTags_LeavesCleanPlainText(string input, string expected)
    {
        // Act
        var result = HtmlHelper.StripToPlainText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Bạch cầu < 4.0 và SpO2 > 95%", "Bạch cầu < 4.0 và SpO2 > 95%")]
    [InlineData("Thân nhiệt < 37.5 độ C, HA > 140/90 mmHg", "Thân nhiệt < 37.5 độ C, HA > 140/90 mmHg")]
    [InlineData("Chỉ số PSA < 2.5 ng/ml & glucose > 7.0 mmol/L", "Chỉ số PSA < 2.5 ng/ml & glucose > 7.0 mmol/L")]
    [InlineData("Kích thước nang < 5mm, không tăng sinh mạch máu", "Kích thước nang < 5mm, không tăng sinh mạch máu")]
    public void TC08_HtmlHelper_PreservesMedicalInequalityOperators(string input, string expected)
    {
        // Act
        var result = HtmlHelper.StripToPlainText(input);

        // Assert: Medical characters < and > are 100% preserved
        Assert.Equal(expected, result);

        // StripTags should also produce the same output
        Assert.Equal(expected, HtmlHelper.StripTags(input));
    }

    [Fact]
    public void TC08_HtmlHelper_NullOrEmptyInput_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, HtmlHelper.StripToPlainText(null));
        Assert.Equal(string.Empty, HtmlHelper.StripToPlainText(""));
        Assert.Equal(string.Empty, HtmlHelper.StripToPlainText("     "));
        Assert.Equal(string.Empty, HtmlHelper.StripTags(null));
    }

    #endregion

    #region TC-09: CreateFromBookingAsync Null Guard

    [Fact]
    public async Task TC09_CreateFromBookingAsync_NullSymptoms_CreatesCaseWithEmptySymptoms_DoesNotThrow()
    {
        // Arrange
        var mockCases = new Mock<ICaseRepository>();
        var mockImages = new Mock<IUltrasoundImageRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();
        var mockStorage = new Mock<IFileStorageService>();
        var mockNotifications = new Mock<INotificationService>();

        Case? createdCase = null;
        mockCases.Setup(r => r.CreateAsync(It.IsAny<Case>(), It.IsAny<CancellationToken>()))
            .Callback<Case, CancellationToken>((c, _) => createdCase = c)
            .ReturnsAsync((Case c, CancellationToken _) => c);

        mockProfiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientProfile
            {
                PatientProfileId = _patientProfileId,
                FullName = "Bệnh nhân Test"
            });

        var caseService = new CaseService(
            mockCases.Object,
            mockImages.Object,
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => mockStorage.Object),
            mockNotifications.Object,
            Mock.Of<ILogger<CaseService>>());

        // Act: Call CreateFromBookingAsync with symptoms = null
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var caseId = await caseService.CreateFromBookingAsync(
            _patientProfileId,
            _doctorId,
            visitDate,
            symptoms: null,
            ct: TestContext.Current.CancellationToken);

        // Assert: No exception thrown, case created with Booked status and empty CaseSymptoms
        Assert.NotEqual(Guid.Empty, caseId);
        Assert.NotNull(createdCase);
        Assert.Equal(CaseStatus.Booked, createdCase.Status);
        Assert.NotNull(createdCase.CaseSymptoms);
        Assert.Empty(createdCase.CaseSymptoms);
    }

    [Fact]
    public async Task TC09_CreateFromBookingAsync_WithSymptoms_PopulatesCaseSymptoms()
    {
        // Arrange
        var mockCases = new Mock<ICaseRepository>();
        var mockImages = new Mock<IUltrasoundImageRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();
        var mockStorage = new Mock<IFileStorageService>();
        var mockNotifications = new Mock<INotificationService>();

        Case? createdCase = null;
        mockCases.Setup(r => r.CreateAsync(It.IsAny<Case>(), It.IsAny<CancellationToken>()))
            .Callback<Case, CancellationToken>((c, _) => createdCase = c)
            .ReturnsAsync((Case c, CancellationToken _) => c);

        mockProfiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientProfile { PatientProfileId = _patientProfileId });

        var caseService = new CaseService(
            mockCases.Object,
            mockImages.Object,
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => mockStorage.Object),
            mockNotifications.Object,
            Mock.Of<ILogger<CaseService>>());

        var symptoms = new List<SymptomInput>
        {
            new SymptomInput { CategoryId = Guid.NewGuid(), SymptomId = Guid.NewGuid(), OtherNote = "Đau bụng hạ vị" },
            new SymptomInput { CategoryId = Guid.NewGuid(), SymptomId = Guid.NewGuid(), OtherNote = "Tiểu buốt" }
        };

        // Act
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var caseId = await caseService.CreateFromBookingAsync(
            _patientProfileId,
            _doctorId,
            visitDate,
            symptoms,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(createdCase);
        Assert.Equal(2, createdCase.CaseSymptoms.Count);
        Assert.Contains(createdCase.CaseSymptoms, s => s.OtherNote == "Đau bụng hạ vị");
        Assert.Contains(createdCase.CaseSymptoms, s => s.OtherNote == "Tiểu buốt");
    }

    #endregion
}
