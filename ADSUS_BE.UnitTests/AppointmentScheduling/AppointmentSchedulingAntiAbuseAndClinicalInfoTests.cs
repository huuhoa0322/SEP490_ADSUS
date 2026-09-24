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
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
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
/// Comprehensive Unit Tests for:
/// 1. Anti-Abuse Dual-Check (User limit & Profile limit)
/// 2. Staff Override bypass anti-abuse
/// 3. System cancellation exclusion (Doctor leave, Reschedule)
/// 4. Update Clinical Info (reason, symptoms, case creation)
/// 5. Booker authorization without personal profile
/// 6. GetCancellationStatusToday
/// </summary>
public class AppointmentSchedulingAntiAbuseAndClinicalInfoTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICaseService> _caseService = new();
    private readonly AppointmentService _appointmentService;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _userProfileId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();

    public AppointmentSchedulingAntiAbuseAndClinicalInfoTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        // Setup Doctor
        var doctor = new User
        {
            UserId = _doctorId,
            FullName = "BS. Trần Văn Hùng",
            Phone = "0988111222",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(doctor);

        // Setup Current User & Profile
        var currentUser = new User
        {
            UserId = _userId,
            FullName = "Nguyễn Văn An",
            Phone = "0912345678",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var userProfile = new PatientProfile
        {
            PatientProfileId = _userProfileId,
            UserId = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(currentUser);
        _db.PatientProfiles.Add(userProfile);
        _db.SaveChanges();

        _profileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.PatientProfiles.Find(id));

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Callback<Appointment, CancellationToken>((appt, _) => _db.Appointments.Add(appt))
            .ReturnsAsync((Appointment appt, CancellationToken _) => appt);

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.ScheduleSlots
                .Include(s => s.Doctor)
                .Include(s => s.Appointments)
                .FirstOrDefault(s => s.SlotId == id));

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _caseService.Setup(c => c.CreateFromBookingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<SymptomInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var noShowService = NoShowTestServices.Create(_db, _notificationService.Object);

        _appointmentService = new AppointmentService(
            _appointmentRepo.BackedBy(_db).Object,
            _slotRepo.BackedBy(_db).Object,
            new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db),
            new ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService(_profileRepo.Object, new ADSUS_BE.DAL.Repositories.Implementations.UserRepository(_db), Microsoft.Extensions.Logging.Abstractions.NullLogger<ADSUS_BE.BLL.MedicalRecord.Services.PatientProfileService>.Instance),
            PatientAccountTestServices.Relationship(_db),
            _notificationService.Object,
            _caseService.BackedBy(_db).Object,
            noShowService,
            new ADSUS_BE.DAL.Repositories.Implementations.UnitOfWork(_db),
            Mock.Of<ILogger<AppointmentService>>(),
            _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private ScheduleSlot CreateOpenSlot(DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctorId,
            SlotDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Status = SlotStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        _db.ScheduleSlots.Add(slot);
        _db.SaveChanges();
        return slot;
    }

    // =========================================================================
    // 1. Anti-Abuse Dual-Check (User limit & Profile limit)
    // =========================================================================

    [Fact]
    public async Task AntiAbuse_UserHasThreeCancellationsToday_ThrowsInvalidOperationException()
    {
        // Arrange: Tạo 3 lượt hủy hôm nay của _userId
        var todayUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());
        for (int i = 0; i < 3; i++)
        {
            var oldSlot = CreateOpenSlot(ClinicClock.Today().AddDays(1 + i), new TimeOnly(9, 0), new TimeOnly(9, 30));
            var cancelledAppt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = oldSlot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = _userId,
                Status = AppointmentStatus.Cancelled,
                CancelledReason = "Bận việc đột xuất",
                CreatedAt = todayUtc.AddHours(1),
                UpdatedAt = todayUtc.AddHours(2)
            };
            _db.Appointments.Add(cancelledAppt);
        }
        _db.SaveChanges();

        var targetSlot = CreateOpenSlot(ClinicClock.Today().AddDays(5), new TimeOnly(10, 0), new TimeOnly(10, 30));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            Reason = "Khám tổng quát"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, isStaffOverride: false, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Bạn đã hủy lịch 3 lần trong ngày hôm nay", ex.Message);
    }

    [Fact]
    public async Task AntiAbuse_PatientProfileHasThreeCancellationsToday_ThrowsInvalidOperationException()
    {
        // Arrange: Người thân B có profile riêng, đã bị hủy 3 lần hôm nay bởi người khác
        var relativeProfileId = Guid.NewGuid();
        var relativeProfile = new PatientProfile
        {
            PatientProfileId = relativeProfileId,
            FullName = "Nguyễn Thị Con",
            Phone = "0933444555",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(relativeProfile);

        // Mối quan hệ giữa _userId và relativeProfile
        var relationshipId = Guid.NewGuid();
        var rel = new PatientRelationship
        {
            RelationshipId = relationshipId,
            UserId = _userId,
            PatientProfileId = relativeProfileId,
            RelationshipName = "Con",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(rel);

        var todayUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());
        for (int i = 0; i < 3; i++)
        {
            var oldSlot = CreateOpenSlot(ClinicClock.Today().AddDays(1 + i), new TimeOnly(8, 0), new TimeOnly(8, 30));
            var cancelledAppt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = oldSlot.SlotId,
                PatientProfileId = relativeProfileId,
                BookedByUserId = Guid.NewGuid(), // Người khác đặt rồi hủy
                Status = AppointmentStatus.Cancelled,
                CancelledReason = "Đổi ý",
                CreatedAt = todayUtc.AddHours(1),
                UpdatedAt = todayUtc.AddHours(2)
            };
            _db.Appointments.Add(cancelledAppt);
        }
        _db.SaveChanges();

        var targetSlot = CreateOpenSlot(ClinicClock.Today().AddDays(5), new TimeOnly(14, 0), new TimeOnly(14, 30));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            RelationshipId = relationshipId,
            Reason = "Khám cho con"
        };

        // Act & Assert: _userId chưa hủy lần nào nhưng profile con đã có 3 lần hủy -> bị chặn
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, isStaffOverride: false, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Hồ sơ bệnh nhân này đã có 3 lần hủy lịch trong ngày hôm nay", ex.Message);
    }

    [Fact]
    public async Task AntiAbuse_UserHasTwoCancellationsToday_AllowsBooking()
    {
        // Arrange: Chỉ có 2 lần hủy hôm nay
        var todayUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());
        for (int i = 0; i < 2; i++)
        {
            var oldSlot = CreateOpenSlot(ClinicClock.Today().AddDays(1 + i), new TimeOnly(9, 0), new TimeOnly(9, 30));
            var cancelledAppt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = oldSlot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = _userId,
                Status = AppointmentStatus.Cancelled,
                CancelledReason = "Bận việc",
                CreatedAt = todayUtc.AddHours(1),
                UpdatedAt = todayUtc.AddHours(2)
            };
            _db.Appointments.Add(cancelledAppt);
        }
        _db.SaveChanges();

        var targetSlot = CreateOpenSlot(ClinicClock.Today().AddDays(5), new TimeOnly(10, 0), new TimeOnly(10, 30));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            Reason = "Khám bình thường"
        };

        // Act
        var result = await _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, isStaffOverride: false, ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    // =========================================================================
    // 2. Staff Override bypass anti-abuse
    // =========================================================================

    [Fact]
    public async Task AntiAbuse_StaffOverride_AllowsBookingWhenProfileHasThreeCancellationsToday()
    {
        // Arrange: Profile đã hủy 3 lần hôm nay
        var todayUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());
        for (int i = 0; i < 3; i++)
        {
            var oldSlot = CreateOpenSlot(ClinicClock.Today().AddDays(1 + i), new TimeOnly(9, 0), new TimeOnly(9, 30));
            var cancelledAppt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = oldSlot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = _userId,
                Status = AppointmentStatus.Cancelled,
                CancelledReason = "Bận",
                CreatedAt = todayUtc.AddHours(1),
                UpdatedAt = todayUtc.AddHours(2)
            };
            _db.Appointments.Add(cancelledAppt);
        }
        _db.SaveChanges();

        var targetSlot = CreateOpenSlot(ClinicClock.Today().AddDays(5), new TimeOnly(15, 0), new TimeOnly(15, 30));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            Reason = "Điều dưỡng hỗ trợ đặt qua hotline"
        };

        // Act: Gọi qua overload của Staff (isStaffOverride = true)
        var result = await _appointmentService.BookAppointmentAsync(_userProfileId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    // =========================================================================
    // 3. System cancellation exclusion
    // =========================================================================

    [Fact]
    public async Task AntiAbuse_SystemCancellations_AreNotCountedInDailyLimit()
    {
        // Arrange: 2 lần hủy do bệnh nhân, 1 lần do "Đổi lịch:", 1 lần do "Bác sĩ nghỉ phép"
        var todayUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());

        // 2 lần bệnh nhân hủy
        for (int i = 0; i < 2; i++)
        {
            var slot = CreateOpenSlot(ClinicClock.Today().AddDays(1 + i), new TimeOnly(8, 0), new TimeOnly(8, 30));
            _db.Appointments.Add(new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = _userId,
                Status = AppointmentStatus.Cancelled,
                CancelledReason = "Lý do cá nhân",
                CreatedAt = todayUtc.AddHours(1),
                UpdatedAt = todayUtc.AddHours(2)
            });
        }

        // 1 lần hệ thống đổi lịch
        var reschedSlot = CreateOpenSlot(ClinicClock.Today().AddDays(3), new TimeOnly(8, 0), new TimeOnly(8, 30));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = reschedSlot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = _userId,
            Status = AppointmentStatus.Cancelled,
            CancelledReason = "Đổi lịch: Bác sĩ yêu cầu dời ca",
            CreatedAt = todayUtc.AddHours(1),
            UpdatedAt = todayUtc.AddHours(2)
        });

        // 1 lần bác sĩ nghỉ phép
        var leaveSlot = CreateOpenSlot(ClinicClock.Today().AddDays(4), new TimeOnly(8, 0), new TimeOnly(8, 30));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = leaveSlot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = _userId,
            Status = AppointmentStatus.Cancelled,
            CancelledReason = "Bác sĩ nghỉ phép đột xuất",
            CreatedAt = todayUtc.AddHours(1),
            UpdatedAt = todayUtc.AddHours(2)
        });
        _db.SaveChanges();

        // Act: Kiểm tra trạng thái hủy hôm nay
        var statusResponse = await _appointmentService.GetCancellationStatusTodayAsync(_userId, TestContext.Current.CancellationToken);

        // Assert: Chỉ tính 2 lần, không bị khóa
        Assert.Equal(2, statusResponse.CancellationsToday);
        Assert.True(statusResponse.CanBookOnline);
        Assert.True(statusResponse.IsNextCancellationFinal);

        // Người dùng vẫn đặt lịch thành công
        var targetSlot = CreateOpenSlot(ClinicClock.Today().AddDays(6), new TimeOnly(9, 0), new TimeOnly(9, 30));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            Reason = "Khám kiểm tra"
        };
        var bookResult = await _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, isStaffOverride: false, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(bookResult);
    }

    // =========================================================================
    // 4. Update Clinical Info
    // =========================================================================

    [Fact]
    public async Task UpdateClinicalInfo_BookedAppointment_UpdatesReasonAndSymptoms()
    {
        // Arrange
        var slot = CreateOpenSlot(ClinicClock.Today().AddDays(2), new TimeOnly(10, 0), new TimeOnly(10, 30));
        var category = new SymptomCategory
        {
            CategoryId = Guid.NewGuid(),
            Name = "Tiêu hóa"
        };
        var symptom = new Symptom
        {
            SymptomId = Guid.NewGuid(),
            CategoryId = category.CategoryId,
            Name = "Đau bụng"
        };
        _db.SymptomCategories.Add(category);
        _db.Symptoms.Add(symptom);

        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            PatientProfileId = _userProfileId,
            DoctorId = _doctorId,
            VisitDate = slot.SlotDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Cases.Add(medicalCase);

        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = null, // Tự đặt
            Status = AppointmentStatus.Booked,
            Reason = "Lý do ban đầu",
            CaseId = medicalCase.CaseId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        var updateRequest = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = "Lý do cập nhật: đau bụng dữ dội",
            Symptoms = new List<SymptomInput>
            {
                new()
                {
                    CategoryId = category.CategoryId,
                    SymptomId = symptom.SymptomId,
                    OtherNote = "Đau sau khi ăn"
                }
            }
        };

        // Act
        var response = await _appointmentService.UpdateClinicalInfoAsync(
            appt.AppointmentId, _userId, _userProfileId, updateRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Lý do cập nhật: đau bụng dữ dội", response.Reason);
        var symptomResponse = Assert.Single(response.Symptoms);
        Assert.Equal(category.CategoryId, symptomResponse.CategoryId);
        Assert.Equal("Tiêu hóa", symptomResponse.CategoryName);
        Assert.Equal("Đau bụng", symptomResponse.SymptomName);
        Assert.Equal("Đau sau khi ăn", symptomResponse.OtherNote);

        // DB Assert
        var updatedInDb = await _db.Appointments.FindAsync(new object[] { appt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal("Lý do cập nhật: đau bụng dữ dội", updatedInDb!.Reason);
        var symptomsInDb = await _db.CaseSymptoms.Where(cs => cs.CaseId == medicalCase.CaseId).ToListAsync(TestContext.Current.CancellationToken);
        var symptomInDb = Assert.Single(symptomsInDb);
        Assert.Equal(symptom.SymptomId, symptomInDb.SymptomId);
    }

    /// <summary>Case + 1 triệu chứng cũ + lịch hẹn Booked trỏ tới Case đó.</summary>
    private (Appointment Appointment, Case MedicalCase, SymptomCategory Category) SeedCaseWithOldSymptom()
    {
        var slot = CreateOpenSlot(ClinicClock.Today().AddDays(2), new TimeOnly(10, 0), new TimeOnly(10, 30));
        var category = new SymptomCategory { CategoryId = Guid.NewGuid(), Name = "Tiêu hóa" };
        _db.SymptomCategories.Add(category);
        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            PatientProfileId = _userProfileId,
            DoctorId = _doctorId,
            VisitDate = slot.SlotDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        medicalCase.CaseSymptoms.Add(new CaseSymptom
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            CategoryId = category.CategoryId,
            OtherNote = "Triệu chứng cũ",
            CreatedAt = DateTime.UtcNow
        });
        _db.Cases.Add(medicalCase);
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _userProfileId,
            Status = AppointmentStatus.Booked,
            Reason = "Lý do ban đầu",
            CaseId = medicalCase.CaseId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();
        return (appt, medicalCase, category);
    }

    [Fact]
    public async Task UpdateClinicalInfo_CaseAlreadyHasSymptoms_OldSymptomsReplacedNotAppended()
    {
        // Arrange
        var (appt, medicalCase, category) = SeedCaseWithOldSymptom();
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Symptoms = new List<SymptomInput> { new() { CategoryId = category.CategoryId, OtherNote = "Triệu chứng mới" } }
        };

        // Act
        var response = await _appointmentService.UpdateClinicalInfoAsync(
            appt.AppointmentId, _userId, _userProfileId, request, TestContext.Current.CancellationToken);

        // Assert — response và DB đều chỉ còn triệu chứng mới
        Assert.Equal("Triệu chứng mới", Assert.Single(response.Symptoms).OtherNote);
        _db.ChangeTracker.Clear();
        var inDb = await _db.CaseSymptoms.AsNoTracking().Where(cs => cs.CaseId == medicalCase.CaseId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Triệu chứng mới", Assert.Single(inDb).OtherNote);
    }

    [Fact]
    public async Task UpdateClinicalInfo_SymptomsNotSent_ExistingSymptomsKept()
    {
        // Arrange — chỉ sửa lý do khám, không gửi danh sách triệu chứng
        var (appt, medicalCase, _) = SeedCaseWithOldSymptom();
        var request = new UpdateAppointmentClinicalInfoRequest { Reason = "Chỉ đổi lý do" };

        // Act
        var response = await _appointmentService.UpdateClinicalInfoAsync(
            appt.AppointmentId, _userId, _userProfileId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Chỉ đổi lý do", response.Reason);
        Assert.Equal("Triệu chứng cũ", Assert.Single(response.Symptoms).OtherNote);
        _db.ChangeTracker.Clear();
        var inDb = await _db.CaseSymptoms.AsNoTracking().Where(cs => cs.CaseId == medicalCase.CaseId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Triệu chứng cũ", Assert.Single(inDb).OtherNote);
    }

    [Fact]
    public async Task UpdateClinicalInfo_WhenNoCaseExists_CreatesCaseAndLinksAppointment()
    {
        // Arrange: Appointment chưa có CaseId
        var slot = CreateOpenSlot(ClinicClock.Today().AddDays(2), new TimeOnly(14, 0), new TimeOnly(14, 30));
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = _userId,
            Status = AppointmentStatus.Booked,
            Reason = "Khám họng",
            CaseId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        var categoryId = Guid.NewGuid();
        var generatedCaseId = Guid.NewGuid();
        _caseService.Setup(c => c.CreateFromBookingAsync(
                _userProfileId, _doctorId, slot.SlotDate, It.IsAny<IReadOnlyList<SymptomInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedCaseId);

        var updateRequest = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = "Khám họng hạt",
            Symptoms = new List<SymptomInput>
            {
                new()
                {
                    CategoryId = categoryId,
                    OtherNote = "Rát họng 3 ngày"
                }
            }
        };

        // Act
        var response = await _appointmentService.UpdateClinicalInfoAsync(
            appt.AppointmentId, _userId, _userProfileId, updateRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(generatedCaseId, response.CaseId);
        var dbAppt = await _db.Appointments.FindAsync(new object[] { appt.AppointmentId }, TestContext.Current.CancellationToken);
        Assert.Equal(generatedCaseId, dbAppt!.CaseId);
        _caseService.Verify(c => c.CreateFromBookingAsync(
            _userProfileId, _doctorId, slot.SlotDate, It.IsAny<IReadOnlyList<SymptomInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateClinicalInfo_PastSlotTime_ThrowsInvalidOperationException()
    {
        // Arrange: Slot hôm qua
        var yesterdaySlot = CreateOpenSlot(ClinicClock.Today().AddDays(-1), new TimeOnly(9, 0), new TimeOnly(9, 30));
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = yesterdaySlot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = _userId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        var request = new UpdateAppointmentClinicalInfoRequest { Reason = "Cập nhật" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _appointmentService.UpdateClinicalInfoAsync(appt.AppointmentId, _userId, _userProfileId, request, TestContext.Current.CancellationToken));

        Assert.Contains("Không thể chỉnh sửa thông tin cho lịch hẹn trong quá khứ hoặc đã đến giờ khám", ex.Message);
    }

    [Fact]
    public async Task UpdateClinicalInfo_CancelledAppointment_ThrowsInvalidOperationException()
    {
        // Arrange: Lịch đã bị hủy
        var slot = CreateOpenSlot(ClinicClock.Today().AddDays(2), new TimeOnly(9, 0), new TimeOnly(9, 30));
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = _userId,
            Status = AppointmentStatus.Cancelled,
            CancelledReason = "Hủy lịch",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        var request = new UpdateAppointmentClinicalInfoRequest { Reason = "Cập nhật" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _appointmentService.UpdateClinicalInfoAsync(appt.AppointmentId, _userId, _userProfileId, request, TestContext.Current.CancellationToken));

        Assert.Contains("Chỉ có thể chỉnh sửa thông tin cho lịch hẹn đang ở trạng thái ĐÃ ĐẶT", ex.Message);
    }

    // =========================================================================
    // 5. Booker authorization without personal profile
    // =========================================================================

    [Fact]
    public async Task UpdateClinicalInfo_BookerWithoutPersonalProfile_AllowsUpdate()
    {
        // Arrange: Booker User C không có PatientProfile (callerPatientProfileId = null)
        var bookerUserId = Guid.NewGuid();
        var bookerUser = new User
        {
            UserId = bookerUserId,
            FullName = "Nguyễn Văn Người Đặt Hộ",
            Phone = "0944555666",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(bookerUser);

        // Bệnh nhân được đặt hộ (Profile riêng)
        var targetProfileId = Guid.NewGuid();
        var targetProfile = new PatientProfile
        {
            PatientProfileId = targetProfileId,
            FullName = "Cụ Bà Nguyễn Thị Mẫu",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(targetProfile);

        var slot = CreateOpenSlot(ClinicClock.Today().AddDays(3), new TimeOnly(10, 0), new TimeOnly(10, 30));
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = targetProfileId,
            BookedByUserId = bookerUserId,
            Status = AppointmentStatus.Booked,
            Reason = "Đau lưng",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = "Đau lưng và khớp gối"
        };

        // Act: callerPatientProfileId = null
        var response = await _appointmentService.UpdateClinicalInfoAsync(
            appt.AppointmentId, bookerUserId, callerPatientProfileId: null, request, TestContext.Current.CancellationToken);

        // Assert: Thành công
        Assert.NotNull(response);
        Assert.Equal("Đau lưng và khớp gối", response.Reason);
    }

    [Fact]
    public async Task UpdateClinicalInfo_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var slot = CreateOpenSlot(ClinicClock.Today().AddDays(3), new TimeOnly(10, 0), new TimeOnly(10, 30));
        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            PatientProfileId = _userProfileId,
            BookedByUserId = _userId,
            Status = AppointmentStatus.Booked,
            Reason = "Khám",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();

        var hackerUserId = Guid.NewGuid();
        var hackerProfileId = Guid.NewGuid();
        var request = new UpdateAppointmentClinicalInfoRequest { Reason = "Đổi trộm lý do" };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _appointmentService.UpdateClinicalInfoAsync(
                appt.AppointmentId, hackerUserId, hackerProfileId, request, TestContext.Current.CancellationToken));
    }
}
