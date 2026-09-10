using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Adversarial stress tests for Appointment Checkin Queue (Milestone 1).
/// Tests boundary inputs, injections, empty results, null navigations, and exception safety.
/// </summary>
public class AppointmentCheckinStressTests : IDisposable
{
    private readonly Mock<IAppointmentRepository> _appointmentRepo = new();
    private readonly Mock<IScheduleSlotRepository> _slotRepo = new();
    private readonly Mock<IPatientProfileRepository> _profileRepo = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ADSUS_BE.BLL.MedicalRecord.Interfaces.ICaseService> _caseService = new();
    private readonly NoShowService _noShowService;
    private readonly AppDbContext _db;
    private readonly AppointmentService _sut;
    private readonly AppointmentsController _controller;

    public AppointmentCheckinStressTests()
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

        _controller = new AppointmentsController(_sut, _profileRepo.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedStandardAppointmentsAsync(DateOnly date, int count = 5)
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Dr. Stress Tester",
            Phone = "0900000001",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
        };
        _db.Users.Add(doctor);

        for (int i = 0; i < count; i++)
        {
            var patient = new User
            {
                UserId = Guid.NewGuid(),
                FullName = $"Patient {i:00} Nguyen Van A",
                Phone = $"09110000{i:00}",
                PasswordHash = "hash",
                Role = UserRole.Patient,
                Status = UserStatus.Active,
            };
            var profile = new PatientProfile
            {
                PatientProfileId = Guid.NewGuid(),
                UserId = patient.UserId,
                User = patient,
                CreatedBy = patient.UserId,
            };
            var startHour = 8 + (i / 2);
            var startMin = (i % 2) * 30;
            var startTime = new TimeOnly(startHour, startMin);
            var endTime = startTime.AddMinutes(30);
            var slot = new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = doctor.UserId,
                Doctor = doctor,
                SlotDate = date,
                StartTime = startTime,
                EndTime = endTime,
                Status = SlotStatus.Booked,
            };
            var appt = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                SlotId = slot.SlotId,
                Slot = slot,
                PatientProfileId = profile.PatientProfileId,
                PatientProfile = profile,
                Status = i % 2 == 0 ? AppointmentStatus.Booked : AppointmentStatus.Approved,
                Reason = i == 0 ? "General Checkup" : $"Routine Check {i}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.Users.Add(patient);
            _db.PatientProfiles.Add(profile);
            _db.ScheduleSlots.Add(slot);
            _db.Appointments.Add(appt);
        }

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_FromDateGreaterThanToDate_SwapsDatesAndReturnsData()
    {
        var targetDate = new DateOnly(2026, 9, 15);
        await SeedStandardAppointmentsAsync(targetDate, 3);

        var earlierDate = targetDate.AddDays(-5);
        var laterDate = targetDate.AddDays(5);

        // Inverted: fromDate is laterDate, toDate is earlierDate
        var result = await _sut.GetCheckinQueueAsync(
            fromDate: laterDate,
            toDate: earlierDate,
            search: null,
            status: null,
            page: 1,
            pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task GetCheckinQueueAsync_PageLessThanOrEqualToZero_ClampedToPageOne(int invalidPage)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 3);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, null, page: invalidPage, pageSize: 15, ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task GetCheckinQueueAsync_PageSizeLessThanOrEqualToZero_ClampedToDefault15(int invalidPageSize)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 3);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, null, page: 1, pageSize: invalidPageSize, ct: TestContext.Current.CancellationToken);

        Assert.Equal(15, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_PageSizeExtreme9999_ClampedToDefault15()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 3);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, null, page: 1, pageSize: 9999, ct: TestContext.Current.CancellationToken);

        Assert.Equal(15, result.PageSize);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_PageBeyondTotalPages_ReturnsEmptyItemsWithoutError()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 3);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, null, null, page: 99999, pageSize: 15, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE \"Appointments\"; --")]
    [InlineData("Robert'); DROP TABLE Students;--")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("%_%")]
    [InlineData(".*")]
    [InlineData("[a-z]")]
    [InlineData("🏥 👨‍⚕️ 💉 💊")]
    public async Task GetCheckinQueueAsync_InjectionAndSpecialCharacters_HandledSafelyWithoutCrash(string maliciousSearch)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 2);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, search: maliciousSearch, status: null, page: 1, pageSize: 15, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetCheckinQueueAsync_NullNavigationsOnEntities_DoesNotThrowNullReference()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Dr. NullField",
            Phone = "0900000001",
            PasswordHash = "x",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
        };

        var patient = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Patient NullField",
            Phone = "0900000099",
            PasswordHash = "x",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
        };

        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            SlotDate = today,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            Status = SlotStatus.Booked,
        };

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patient.UserId,
            User = patient,
            CreatedBy = patient.UserId,
        };

        var appt = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = slot.SlotId,
            Slot = slot,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Status = AppointmentStatus.Booked,
            Reason = null, // NULL Reason
            CaseId = null, // NULL CaseId
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Users.AddRange(doctor, patient);
        _db.ScheduleSlots.Add(slot);
        _db.PatientProfiles.Add(profile);
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, search: null, status: null, page: 1, pageSize: 15, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Patient NullField", item.PatientFullName);
        Assert.Equal("0900000099", item.PatientPhone);
        Assert.Equal("Dr. NullField", item.DoctorName);
        Assert.Null(item.Reason);
        Assert.Equal(Guid.Empty, item.CaseId);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")]
    [InlineData("BOOKED")]
    [InlineData("booked")]
    [InlineData("APPROVED")]
    [InlineData("approved")]
    [InlineData("NO_SHOW")]
    [InlineData("no_show")]
    [InlineData("UNKNOWN_STATUS")]
    [InlineData("   ")]
    public async Task GetCheckinQueueAsync_VariousStatusInputs_HandledRobustly(string status)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 4);

        var result = await _sut.GetCheckinQueueAsync(
            today, today, search: null, status: status, page: 1, pageSize: 15, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 0);
    }

    [Fact]
    public async Task GetCheckinQueue_ControllerEndpoint_HandlesBoundaryParametersSuccessfully()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedStandardAppointmentsAsync(today, 3);

        var actionResult = await _controller.GetCheckinQueue(
            fromDate: today.AddDays(5),
            toDate: today.AddDays(-5),
            date: null,
            search: "<script>",
            status: "ALL",
            page: -1,
            pageSize: 0,
            ct: TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ADSUS_BE.BLL.Common.ApiResponse<CheckinQueueResponse>>(okResult.Value);
        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.Page);
        Assert.Equal(15, response.Data.PageSize);
    }
}
