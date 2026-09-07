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
}
