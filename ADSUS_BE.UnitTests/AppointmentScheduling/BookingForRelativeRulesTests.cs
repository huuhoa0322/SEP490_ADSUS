using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Bộ kiểm thử hoàn chỉnh cho các quy tắc đặt lịch cho người thân (Module 8 & Patient Relationship).
/// Bao gồm 7 nhóm Test Case theo kiến trúc và nghiệp vụ:
/// - TC-RULE1: Cấm thêm SĐT đã có tài khoản vào danh bạ, nhưng cho phép nếu SĐT chưa đăng ký.
/// - TC-USER-SELF-3: User tự đặt cho bản thân tối đa 3 lịch active (BOOKED).
/// - TC-USER-OTHERS-3: User đặt hộ người thân tối đa 3 lịch active.
/// - TC-PATIENT-TOTAL-3: 1 Bệnh nhân (PatientProfile) tối đa 3 lịch active trên toàn hệ thống (bất kể ai đặt).
/// - TC-PATIENT-SAME-DAY-1: 1 Bệnh nhân chỉ có tối đa 1 lịch active trong cùng 1 ngày khám.
/// - TC-USER-MULTI-RELATIVES-SAME-DAY: 1 User có thể đặt cho 2 người thân khác nhau trong cùng 1 ngày.
/// - TC-TRUSTED-RELATIONSHIP-BOOKING: Đặt hộ thành công kể cả khi người thân sau này đã đăng ký tài khoản (Trusted Relationship).
/// </summary>
public class BookingForRelativeRulesTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICaseService> _caseService = new();
    private readonly Mock<IPatientRelationshipRepository> _relationshipRepo = new();
    private readonly AppointmentService _appointmentService;
    private readonly PatientRelationshipService _relationshipService;

    // Common IDs
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _userProfileId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();

    public BookingForRelativeRulesTests()
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
            FullName = "BS. Lê Thị Hoa",
            Phone = "0987654321",
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
            FullName = "Nguyễn Văn Chồng",
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

        // Default mock repo responses
        _profileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.PatientProfiles.Find(id));

        _appointmentRepo.Setup(r => r.CreateAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Callback<Appointment, CancellationToken>((appt, _) => _db.Appointments.Add(appt))
            .ReturnsAsync((Appointment appt, CancellationToken _) => appt);

        _slotRepo.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _caseService.Setup(c => c.CreateFromBookingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<SymptomInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var noShowSettings = Options.Create(new NoShowSettings { GraceTimeMinutes = 15 });
        var noShowService = new NoShowService(
            _db,
            noShowSettings,
            _notificationService.Object,
            _profileRepo.Object,
            Mock.Of<ILogger<NoShowService>>());

        _appointmentService = new AppointmentService(
            _appointmentRepo.Object,
            _slotRepo.Object,
            _profileRepo.Object,
            _notificationService.Object,
            _caseService.Object,
            noShowService,
            _db,
            Mock.Of<ILogger<AppointmentService>>());

        _relationshipService = new PatientRelationshipService(
            _relationshipRepo.Object,
            _profileRepo.Object,
            _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private ScheduleSlot CreateSlot(DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = _doctorId,
            SlotDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Status = SlotStatus.Open,
            CreatedAt = DateTime.UtcNow,
            Doctor = _db.Users.Find(_doctorId)!,
            Appointments = new List<Appointment>()
        };
        _db.ScheduleSlots.Add(slot);
        _db.SaveChanges();

        _slotRepo.Setup(r => r.GetByIdForUpdateAsync(slot.SlotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        return slot;
    }

    #region TC-RULE1: Thêm người thân vào danh bạ (Check SĐT)

    [Fact]
    public async Task TC_RULE1_AddRelativeAsync_PhoneAlreadyRegistered_ThrowsInvalidOperationException()
    {
        // Arrange: SĐT đã có tài khoản trong Users
        var registeredPhone = "0900111222";
        _db.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            Phone = registeredPhone,
            FullName = "Người đã có tài khoản",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        _relationshipRepo.Setup(r => r.IsPhoneRegisteredAsync(registeredPhone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new AddRelativeRequest(
            FullName: "Người Thân",
            Phone: registeredPhone,
            DateOfBirth: new DateOnly(1995, 5, 20),
            RelationshipName: "Vợ");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _relationshipService.AddRelativeAsync(request, _userId, TestContext.Current.CancellationToken));

        Assert.Contains("Số điện thoại này đã có tài khoản", ex.Message);
    }

    [Fact]
    public async Task TC_RULE1_AddRelativeAsync_PhoneNotRegistered_SuccessfullyCreatesGuestProfileAndRelationship()
    {
        // Arrange: SĐT chưa có trong Users
        var newPhone = "0999888777";
        _relationshipRepo.Setup(r => r.IsPhoneRegisteredAsync(newPhone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new AddRelativeRequest(
            FullName: "Trần Thị Vợ",
            Phone: newPhone,
            DateOfBirth: new DateOnly(1996, 6, 15),
            RelationshipName: "Vợ");

        // Act
        var result = await _relationshipService.AddRelativeAsync(request, _userId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Trần Thị Vợ", result.PatientName);
        Assert.Equal("Vợ", result.RelationshipName);

        // Verify guest profile created with UserId == null
        var createdProfile = await _db.PatientProfiles.FindAsync(new object[] { result.PatientProfileId }, TestContext.Current.CancellationToken);
        Assert.NotNull(createdProfile);
        Assert.Null(createdProfile.UserId);
        Assert.Equal(newPhone, createdProfile.Phone);
    }

    #endregion

    #region TC-USER-SELF-3: User tự đặt cho bản thân tối đa 3 lịch active

    [Fact]
    public async Task TC_USER_SELF_3_UserSelfBooking_ReachesLimitOf3Active_FourthBookingThrows()
    {
        // Arrange: Tạo sẵn 3 lịch active (3 BOOKED) cho chính User
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        for (int i = 0; i < 3; i++)
        {
            var date = futureDate.AddDays(i);
            var slot = CreateSlot(date, new TimeOnly(8 + i, 0), new TimeOnly(9 + i, 0));
            _db.Appointments.Add(new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                PatientProfileId = _userProfileId,
                BookedByUserId = null,
                RelationshipId = null,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        _db.SaveChanges();

        // Target slot thứ 4
        var targetSlot = CreateSlot(futureDate.AddDays(4), new TimeOnly(14, 0), new TimeOnly(15, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            RelationshipId = null // Tự đặt cho mình
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("3 lịch hẹn đang chờ", ex.Message);
    }

    #endregion

    #region TC-USER-OTHERS-3: User đặt hộ tối đa 3 lịch active

    [Fact]
    public async Task TC_USER_OTHERS_3_UserBookingForOthers_ReachesLimitOf3Active_FourthBookingThrows()
    {
        // Arrange: Tạo 3 người thân khác nhau
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        for (int i = 0; i < 3; i++)
        {
            var relProfile = new PatientProfile
            {
                PatientProfileId = Guid.NewGuid(),
                FullName = $"Người thân {i}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var rel = new PatientRelationship
            {
                RelationshipId = Guid.NewGuid(),
                UserId = _userId,
                PatientProfileId = relProfile.PatientProfileId,
                RelationshipName = $"Người thân {i}",
                CreatedAt = DateTime.UtcNow
            };
            _db.PatientProfiles.Add(relProfile);
            _db.PatientRelationships.Add(rel);

            var slot = CreateSlot(futureDate.AddDays(i), new TimeOnly(8, 0), new TimeOnly(9, 0));
            _db.Appointments.Add(new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                PatientProfileId = relProfile.PatientProfileId,
                BookedByUserId = _userId,
                RelationshipId = rel.RelationshipId,
                Status = AppointmentStatus.Booked,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        _db.SaveChanges();

        // Tạo người thân thứ 4 để đặt hộ
        var fourthProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            FullName = "Người thân thứ 4",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var fourthRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = fourthProfile.PatientProfileId,
            RelationshipName = "Em gái",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(fourthProfile);
        _db.PatientRelationships.Add(fourthRel);
        _db.SaveChanges();

        var targetSlot = CreateSlot(futureDate.AddDays(5), new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = targetSlot.SlotId,
            RelationshipId = fourthRel.RelationshipId
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("hộ người thân", ex.Message);
    }

    #endregion

    #region TC-PATIENT-TOTAL-3: Bệnh nhân tối đa 3 lịch active trên toàn hệ thống

    [Fact]
    public async Task TC_PATIENT_TOTAL_3_PatientProfileReachesLimitOf3Active_EvenAcrossMultipleUsers_Throws()
    {
        // Arrange: Bệnh nhân Vợ đã có 3 lịch active:
        // - 1 lịch do Vợ tự đặt (Booked)
        // - 1 lịch do Chồng đặt hộ (Booked)
        // - 1 lịch do Con gái đặt hộ (Booked)
        var wifeProfileId = Guid.NewGuid();
        var wifeProfile = new PatientProfile
        {
            PatientProfileId = wifeProfileId,
            FullName = "Nguyễn Thị Vợ",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(wifeProfile);

        var daughterUserId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));

        // Lịch 1: Vợ tự đặt
        var slot1 = CreateSlot(futureDate, new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot1.SlotId,
            PatientProfileId = wifeProfileId,
            BookedByUserId = null,
            RelationshipId = null,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Lịch 2: Chồng đặt hộ
        var husbandRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = wifeProfileId,
            RelationshipName = "Vợ",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(husbandRel);
        var slot2 = CreateSlot(futureDate.AddDays(1), new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot2.SlotId,
            PatientProfileId = wifeProfileId,
            BookedByUserId = _userId,
            RelationshipId = husbandRel.RelationshipId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Lịch 3: Con gái đặt hộ
        var daughterRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = daughterUserId,
            PatientProfileId = wifeProfileId,
            RelationshipName = "Mẹ",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(daughterRel);
        var slot3 = CreateSlot(futureDate.AddDays(2), new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot3.SlotId,
            PatientProfileId = wifeProfileId,
            BookedByUserId = daughterUserId,
            RelationshipId = daughterRel.RelationshipId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        // Chồng mới chỉ đặt 1 lịch hộ (chưa chạm giới hạn 3 lịch của User),
        // nhưng Bệnh nhân Vợ đã có 3 lịch active -> Chồng cố đặt thêm lịch thứ 4 cho Vợ sẽ bị chặn theo quy tắc PatientProfile!
        var slot4 = CreateSlot(futureDate.AddDays(3), new TimeOnly(8, 0), new TimeOnly(9, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot4.SlotId,
            RelationshipId = husbandRel.RelationshipId
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Bệnh nhân này đã có tối đa 3 lịch hẹn", ex.Message);
    }

    #endregion

    #region TC-PATIENT-SAME-DAY-1: 1 Bệnh nhân chỉ có tối đa 1 lịch active trong cùng 1 ngày

    [Fact]
    public async Task TC_PATIENT_SAME_DAY_1_PatientAlreadyHasActiveAppointmentOnSameDate_Throws()
    {
        // Arrange: Bệnh nhân đã có 1 lịch active vào ngày X (8h)
        var sameDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var wifeProfileId = Guid.NewGuid();
        var wifeProfile = new PatientProfile
        {
            PatientProfileId = wifeProfileId,
            FullName = "Nguyễn Thị Vợ",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(wifeProfile);

        var husbandRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = wifeProfileId,
            RelationshipName = "Vợ",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(husbandRel);

        var morningSlot = CreateSlot(sameDate, new TimeOnly(8, 0), new TimeOnly(9, 0));
        _db.Appointments.Add(new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = morningSlot.SlotId,
            PatientProfileId = wifeProfileId,
            BookedByUserId = _userId,
            RelationshipId = husbandRel.RelationshipId,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Slot = morningSlot
        });
        _db.SaveChanges();

        // Chồng cố đặt tiếp cho Vợ lúc 14h chiều cùng ngày
        var afternoonSlot = CreateSlot(sameDate, new TimeOnly(14, 0), new TimeOnly(15, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = afternoonSlot.SlotId,
            RelationshipId = husbandRel.RelationshipId
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken));

        Assert.Contains("Mỗi ngày chỉ được đặt tối đa 1 lịch", ex.Message);
    }

    #endregion

    #region TC-USER-MULTI-RELATIVES-SAME-DAY: 1 User được đặt cho 2 người thân khác nhau cùng ngày

    [Fact]
    public async Task TC_USER_MULTI_RELATIVES_SAME_DAY_UserBooksForTwoDifferentRelativesOnSameDay_Succeeds()
    {
        // Arrange: User có 2 người thân: Vợ và Con
        var wifeProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            FullName = "Vợ Yêu",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var childProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            FullName = "Con Gái",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var wifeRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = wifeProfile.PatientProfileId,
            RelationshipName = "Vợ",
            CreatedAt = DateTime.UtcNow
        };
        var childRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = childProfile.PatientProfileId,
            RelationshipName = "Con",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.AddRange(wifeProfile, childProfile);
        _db.PatientRelationships.AddRange(wifeRel, childRel);
        _db.SaveChanges();

        var sameDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var slotWife = CreateSlot(sameDate, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var slotChild = CreateSlot(sameDate, new TimeOnly(10, 0), new TimeOnly(11, 0));

        // Act 1: Đặt cho Vợ lúc 8h sáng
        var resWife = await _appointmentService.BookAppointmentAsync(
            _userId,
            _userProfileId,
            new BookAppointmentRequest
            {
                ScheduleSlotId = slotWife.SlotId,
                RelationshipId = wifeRel.RelationshipId
            },
            ct: TestContext.Current.CancellationToken);

        Assert.NotNull(resWife);
        Assert.Equal(AppointmentStatus.Booked, resWife.Status);

        // Act 2: Đặt cho Con lúc 10h sáng CÙNG NGÀY -> Phải THÀNH CÔNG vì 2 người là 2 PatientProfile khác nhau!
        var resChild = await _appointmentService.BookAppointmentAsync(
            _userId,
            _userProfileId,
            new BookAppointmentRequest
            {
                ScheduleSlotId = slotChild.SlotId,
                RelationshipId = childRel.RelationshipId
            },
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(resChild);
        Assert.Equal(AppointmentStatus.Booked, resChild.Status);
    }

    #endregion

    #region TC-TRUSTED-RELATIONSHIP-BOOKING: Đặt hộ thành công kể cả khi người thân đã có account

    [Fact]
    public async Task TC_TRUSTED_RELATIONSHIP_BOOKING_RelativeLaterRegisteredAccount_BookingStillSucceedsBecauseTrustedRelationship()
    {
        // Arrange: Người thân lúc thêm vào là guest, sau đó đã đăng ký tài khoản (UserId != null)
        var relativeUserId = Guid.NewGuid();
        var relativeUser = new User
        {
            UserId = relativeUserId,
            FullName = "Nguyễn Thị Vợ Đã Có Nick",
            Phone = "0933444555",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(relativeUser);

        var relativeProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = relativeUserId, // ĐÃ CÓ USER ID
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(relativeProfile);

        // Đã có relationship từ trước (trusted)
        var trustedRel = new PatientRelationship
        {
            RelationshipId = Guid.NewGuid(),
            UserId = _userId,
            PatientProfileId = relativeProfile.PatientProfileId,
            RelationshipName = "Vợ",
            CreatedAt = DateTime.UtcNow
        };
        _db.PatientRelationships.Add(trustedRel);
        _db.SaveChanges();

        var slot = CreateSlot(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)), new TimeOnly(15, 0), new TimeOnly(16, 0));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = slot.SlotId,
            RelationshipId = trustedRel.RelationshipId
        };

        // Act: Đặt lịch cho người thân đã có tài khoản
        var result = await _appointmentService.BookAppointmentAsync(_userId, _userProfileId, request, ct: TestContext.Current.CancellationToken);

        // Assert: Thành công, không bị throw "Người thân này đã có tài khoản"
        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.True(result.IsBookedForOthers);
    }

    #endregion

    #region TC-ELDERLY-PHONE-NULL: Thêm người cao tuổi không có SĐT

    [Fact]
    public async Task TC_ELDERLY_PHONE_NULL_CreatesProfileWithCreatedBy()
    {
        // Arrange: Thêm người thân cao tuổi với Phone = null
        var request = new AddRelativeRequest(
            FullName: "Bà Ngoại",
            Phone: null,
            DateOfBirth: new DateOnly(1945, 1, 1),
            RelationshipName: "Bà Ngoại");

        // Act
        var result = await _relationshipService.AddRelativeAsync(request, _userId, TestContext.Current.CancellationToken);

        // Assert: Trả về kết quả hợp lệ, PatientPhone là null
        Assert.NotNull(result);
        Assert.Equal("Bà Ngoại", result.PatientName);
        Assert.Null(result.PatientPhone);

        // Hồ sơ được tạo độc lập với CreatedBy = userId, Phone = null, UserId = null
        var profile = await _db.PatientProfiles.FindAsync(new object[] { result.PatientProfileId }, TestContext.Current.CancellationToken);
        Assert.NotNull(profile);
        Assert.Null(profile.UserId);
        Assert.Null(profile.Phone);
        Assert.Equal(_userId, profile.CreatedBy);

        // Mối quan hệ được lưu đúng
        var rel = await _db.PatientRelationships.FirstOrDefaultAsync(
            r => r.UserId == _userId && r.PatientProfileId == result.PatientProfileId, TestContext.Current.CancellationToken);
        Assert.NotNull(rel);
        Assert.Equal("Bà Ngoại", rel.RelationshipName);
    }

    #endregion

    #region TC-ELDERLY-ANTI-MERGE: Chống gộp profile người cao tuổi không có SĐT

    [Fact]
    public async Task TC_ELDERLY_ANTI_MERGE_NullPhoneTwoRelatives_DistinctProfiles()
    {
        // Arrange: Thêm 2 người thân cao tuổi đều có Phone = null
        var request1 = new AddRelativeRequest(
            FullName: "Bà Ngoại",
            Phone: null,
            DateOfBirth: new DateOnly(1945, 1, 1),
            RelationshipName: "Bà Ngoại");

        var request2 = new AddRelativeRequest(
            FullName: "Ông Ngoại",
            Phone: null,
            DateOfBirth: new DateOnly(1940, 2, 2),
            RelationshipName: "Ông Ngoại");

        // Act
        var result1 = await _relationshipService.AddRelativeAsync(request1, _userId, TestContext.Current.CancellationToken);
        var result2 = await _relationshipService.AddRelativeAsync(request2, _userId, TestContext.Current.CancellationToken);

        // Assert: Phải tạo ra 2 PatientProfile ID hoàn toàn khác nhau (không được merge profile null phone)
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1.PatientProfileId, result2.PatientProfileId);

        var profile1 = await _db.PatientProfiles.FindAsync(new object[] { result1.PatientProfileId }, TestContext.Current.CancellationToken);
        var profile2 = await _db.PatientProfiles.FindAsync(new object[] { result2.PatientProfileId }, TestContext.Current.CancellationToken);

        Assert.NotNull(profile1);
        Assert.NotNull(profile2);
        Assert.NotEqual(profile1.PatientProfileId, profile2.PatientProfileId);
        Assert.Equal("Bà Ngoại", profile1.FullName);
        Assert.Equal("Ông Ngoại", profile2.FullName);
        Assert.Equal(_userId, profile1.CreatedBy);
        Assert.Equal(_userId, profile2.CreatedBy);
        Assert.Null(profile1.Phone);
        Assert.Null(profile2.Phone);
    }

    #endregion

    #region TC-ACCOUNT-LINKING: Tự động liên kết tài khoản khi người thân đăng ký tài khoản mới

    [Fact]
    public async Task TC_ACCOUNT_LINKING_RegisterPhone_PessimisticLock_Links()
    {
        // Arrange: Đã tồn tại 1 guest profile có SĐT "0988111222" và UserId == null
        var guestPhone = "0988111222";
        var guestProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            FullName = "Khách Hàng Chưa Có Nick",
            Phone = guestPhone,
            UserId = null,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(guestProfile);
        _db.SaveChanges();

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(u => u.PhoneExistsAsync(guestPhone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mockUserRepo.Setup(u => u.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _db.Users.Find(id));

        var mockTokenRepo = new Mock<IRefreshTokenRepository>();
        mockTokenRepo.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockJwtService = new Mock<IJwtTokenService>();
        mockJwtService.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("mock_access_token");

        var authService = new AuthService(
            mockUserRepo.Object,
            mockTokenRepo.Object,
            mockJwtService.Object,
            _db,
            Mock.Of<ILogger<AuthService>>());

        var registerRequest = new RegisterRequest
        {
            PhoneNumber = guestPhone,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FullName = "Nguyễn Văn Đã Đăng Ký"
        };

        // Act: Người thân dùng đúng SĐT đó đăng ký tài khoản
        var (result, response) = await authService.RegisterAsync(registerRequest, TestContext.Current.CancellationToken);

        // Assert: Đăng ký thành công và tự động liên kết guest profile
        Assert.Equal(RegisterResult.Success, result);
        Assert.NotNull(response);

        var linkedProfile = await _db.PatientProfiles.FindAsync(new object[] { guestProfile.PatientProfileId }, TestContext.Current.CancellationToken);
        Assert.NotNull(linkedProfile);
        Assert.Equal(response.UserId, linkedProfile.UserId);
        Assert.Null(linkedProfile.Phone); // Guest phone cleared
        Assert.Null(linkedProfile.FullName); // Guest name cleared
    }

    #endregion

    #region TC-REMINDER-ROUTING: Định tuyến thông báo nhắc lịch về BookedByUserId

    [Fact]
    public async Task TC_REMINDER_ROUTING_RoutesToBookedByUserId()
    {
        // Arrange: Cuộc hẹn đặt cho người thân cao tuổi (không có tài khoản, UserId == null)
        var elderlyProfileId = Guid.NewGuid();
        var elderlyProfile = new PatientProfile
        {
            PatientProfileId = elderlyProfileId,
            FullName = "Bà Ngoại",
            UserId = null,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PatientProfiles.Add(elderlyProfile);

        // Slot hẹn vào 22 giờ tới (nằm trong khung 20-24h nhắc hẹn)
        var appointmentTime = DateTime.UtcNow.AddHours(22);
        var slot = CreateSlot(
            DateOnly.FromDateTime(appointmentTime),
            TimeOnly.FromDateTime(appointmentTime),
            TimeOnly.FromDateTime(appointmentTime.AddMinutes(30)));

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = elderlyProfileId,
            BookedByUserId = _userId, // Người đặt hộ
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appointment);
        _db.SaveChanges();

        // Setup mock repos cho Reminder Job
        var patientRow = new PatientListRow(
            PatientProfileId: elderlyProfileId,
            PatientUserId: Guid.Empty, // Bệnh nhân chưa có user_id
            FullName: "Bà Ngoại",
            Phone: "0900000000",
            LatestVisitDate: null,
            LatestVisitStatus: null);

        var jobProfileRepo = new Mock<IPatientProfileRepository>();
        jobProfileRepo.Setup(r => r.SearchAsync(null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PatientListRow> { patientRow }, 1));

        var jobAppointmentRepo = new Mock<IAppointmentRepository>();
        jobAppointmentRepo.Setup(r => r.ListByPatientAsync(elderlyProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var reminderNotificationMock = new Mock<INotificationService>();
        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(INotificationService)))
            .Returns(reminderNotificationMock.Object);
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        var reminderJob = new AppointmentReminderJob(
            scopeFactory.Object,
            jobProfileRepo.Object,
            jobAppointmentRepo.Object,
            Mock.Of<ILogger<AppointmentReminderJob>>());

        var jobContext = new Mock<IJobExecutionContext>();

        // Act
        await reminderJob.Execute(jobContext.Object);

        // Assert: Notification được gửi tới _userId (BookedByUserId), không bị crash vì PatientUserId là null/Empty
        reminderNotificationMock.Verify(n => n.SendAsync(
            It.Is<SendNotificationRequest>(req => req.UserId == _userId && req.Type == "appointment_reminder"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TC-EF-MODEL-NO-SHADOW-PROPERTIES: Kiểm tra compiled EF Model không còn Shadow Properties

    [Fact]
    public void TC_EF_MODEL_NO_SHADOW_PROPERTIES_ZeroShadowProperties()
    {
        // Act: Kiểm tra các entity type Appointment và PatientRelationship trên compiled model
        var appointmentEntityType = _db.Model.FindEntityType(typeof(Appointment));
        Assert.NotNull(appointmentEntityType);
        var appointmentShadowProps = appointmentEntityType.GetProperties()
            .Where(p => p.IsShadowProperty())
            .Select(p => p.Name)
            .ToList();

        var relEntityType = _db.Model.FindEntityType(typeof(PatientRelationship));
        Assert.NotNull(relEntityType);
        var relShadowProps = relEntityType.GetProperties()
            .Where(p => p.IsShadowProperty())
            .Select(p => p.Name)
            .ToList();

        // Assert: Không còn bất kỳ shadow property nào (như UserId, PatientRelationshipRelationshipId, UserId1, PatientProfileId1)
        Assert.Empty(appointmentShadowProps);
        Assert.Empty(relShadowProps);
    }

    #endregion
}
