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
/// Unit tests cho SlotGeneratorJob (JOB-02).
/// Tạo slots tự động cho 14 ngày tới.
/// </summary>
public class SlotGeneratorJobTests : IDisposable
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<ILogger<SlotGeneratorJob>> _logger = new();
    private readonly AppDbContext _db;
    private readonly SlotGeneratorJob _sut;

    public SlotGeneratorJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var serviceScope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(_db);
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);

        _slotRepo.Setup(r => r.ListTimeRangesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        _sut = new SlotGeneratorJob(
            _userRepo.Object,
            _slotRepo.Object,
            _logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private static User CreateDoctor(string name = "Dr. Test")
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = name,
            Phone = "0900000001",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
        };
    }

    private static IJobExecutionContext CreateMockJobExecutionContext()
    {
        var context = new Mock<IJobExecutionContext>();
        return context.Object;
    }

    #endregion

    #region TC-001: No Doctors

    [Fact]
    public async Task Execute_NoDoctors_DoesNothing()
    {
        // Arrange
        _userRepo.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Không có slot nào được tạo
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region TC-002: Creates Slots for Doctor

    [Fact]
    public async Task Execute_DoctorExists_CreatesSlots()
    {
        // Arrange
        var doctor = CreateDoctor();
        _userRepo.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { doctor });

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Slots được tạo cho bác sĩ
        _slotRepo.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion

    #region TC-003: Multiple Doctors

    [Fact]
    public async Task Execute_MultipleDoctors_CreatesSlotsForAll()
    {
        // Arrange
        var doctor1 = CreateDoctor("Dr. One");
        var doctor2 = CreateDoctor("Dr. Two");
        _userRepo.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { doctor1, doctor2 });

        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        var context = CreateMockJobExecutionContext();

        // Act
        await _sut.Execute(context);

        // Assert - Slots được tạo cho cả 2 bác sĩ
        _slotRepo.Verify(r => r.AddAsync(
            It.Is<ScheduleSlot>(s => s.DoctorId == doctor1.UserId),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _slotRepo.Verify(r => r.AddAsync(
            It.Is<ScheduleSlot>(s => s.DoctorId == doctor2.UserId),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion

    #region TC-004: Existing Slots — Overlap Checked In Memory

    [Fact]
    public async Task Execute_ExistingSlots_SkipsOverlapsWithOneQueryForAllDoctors()
    {
        // Arrange — bác sĩ 1 đã có 1 slot CLOSED 08:00-09:00 ngày mai (đè 2 ca 30 phút)
        var doctor1 = CreateDoctor("Dr. One");
        var doctor2 = CreateDoctor("Dr. Two");
        var tomorrow = ClinicClock.Today().AddDays(1);
        _userRepo.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { doctor1, doctor2 });
        _slotRepo.Setup(r => r.ListTimeRangesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>
            {
                new() { DoctorId = doctor1.UserId, SlotDate = tomorrow, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0), Status = SlotStatus.Closed },
            });

        var created = new List<ScheduleSlot>();
        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Callback((ScheduleSlot s, CancellationToken _) => created.Add(s))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        await _sut.Execute(CreateMockJobExecutionContext());

        // Assert — một truy vấn cho cả hai bác sĩ, không còn gọi HasOverlapAsync từng ca
        _slotRepo.Verify(r => r.ListTimeRangesAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(doctor1.UserId) && ids.Contains(doctor2.UserId)),
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once);
        _slotRepo.Verify(r => r.HasOverlapAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);

        // Bác sĩ 1: 2 ca 08:00 và 08:30 ngày mai bị bỏ qua, các ca khác vẫn được tạo
        var doctor1Tomorrow = created.Where(s => s.DoctorId == doctor1.UserId && s.SlotDate == tomorrow).ToList();
        Assert.Equal(14, doctor1Tomorrow.Count);
        Assert.DoesNotContain(doctor1Tomorrow, s => s.StartTime < new TimeOnly(9, 0));
        // Bác sĩ 2 không bị ảnh hưởng bởi slot của bác sĩ 1
        Assert.Equal(16, created.Count(s => s.DoctorId == doctor2.UserId && s.SlotDate == tomorrow));
    }

    #endregion

    #region TC-005: Clinic Local Time

    [Fact]
    public async Task Execute_UsesClinicLocalDate_AndSkipsSlotsAlreadyStartedInLocalTime()
    {
        // Arrange
        var doctor = CreateDoctor();
        _userRepo.Setup(r => r.ListActiveDoctorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { doctor });
        var created = new List<ScheduleSlot>();
        _slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Callback((ScheduleSlot s, CancellationToken _) => created.Add(s))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        await _sut.Execute(CreateMockJobExecutionContext());

        // Assert — SlotDate/StartTime là giờ địa phương UTC+7: không tạo ca đã bắt đầu, và sinh đủ
        // 30 ngày tính từ "hôm nay" theo giờ phòng khám
        var now = DateTime.UtcNow;
        var today = ClinicClock.Today();
        Assert.All(created, s => Assert.True(ClinicClock.StartOfDayUtc(s.SlotDate).Add(s.StartTime.ToTimeSpan()) > now.AddMinutes(-1)));
        Assert.Equal(today.AddDays(29), created.Max(s => s.SlotDate));
        Assert.True(created.Min(s => s.SlotDate) >= today);
        _slotRepo.Verify(r => r.ListTimeRangesAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), today, today.AddDays(29), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
