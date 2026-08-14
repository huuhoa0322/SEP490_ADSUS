using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.AppointmentScheduling;

/// <summary>
/// Integration tests for ScheduleSlotsController (UC-15 - Manage Clinic Schedule).
/// Tests HTTP endpoints with mocked repositories to avoid hitting team database.
///
/// Allowed roles: Doctor only.
/// BR-01: VisitDate + StartTime > now (UTC); range > 15 phút; không overlap.
/// BR-02: Closed có thể mở lại.
/// </summary>
public class ScheduleSlotsControllerIntegrationTests
{
    private readonly Mock<IScheduleSlotRepository> _slots = new();
    private readonly Mock<IUserRepository> _users = new();

    #region Create Schedule Slot Tests

    [Fact]
    public async Task CreateSlot_ValidRequest_ReturnsCreated()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        _slots.Setup(r => r.HasOverlapAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _slots.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/schedule-slots", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScheduleSlotResponse>>();
        Assert.Equal(201, body!.Code);
        Assert.Equal(SlotStatus.Open, body.Data!.Status);
    }

    [Fact]
    public async Task CreateSlot_PastDate_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var request = new CreateScheduleSlotRequest
        {
            VisitDate = pastDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/schedule-slots", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSlot_DurationTooShort_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 10), // Only 10 minutes
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/schedule-slots", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSlot_OverlappingSlot_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        _slots.Setup(r => r.HasOverlapAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Overlap exists

        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/schedule-slots", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Contains("overlap", body!.Message!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Role Authorization Tests

    [Fact]
    public async Task CreateSlot_AsPatient_ReturnsForbidden()
    {
        // Arrange - Patient does NOT have DOCTOR role
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/schedule-slots", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSlot_AsNurse_ReturnsForbidden()
    {
        // Arrange - Nurse does NOT have DOCTOR role
        using var app = CreateApp();
        var client = CreateNurseClient(app);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var request = new CreateScheduleSlotRequest
        {
            VisitDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/schedule-slots", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSlot_NoToken_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // No auth header

        // Act
        var response = await client.GetAsync("/api/v1/schedule-slots");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region List Schedule Slots Tests

    [Fact]
    public async Task ListSlots_ValidDoctor_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);

        _slots.Setup(r => r.ListByRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<Guid>(), It.IsAny<SlotStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        // Act
        var response = await client.GetAsync("/api/v1/schedule-slots");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ScheduleSlotResponse>>>();
        Assert.Equal(200, body!.Code);
    }

    [Fact]
    public async Task ListSlots_WithDateFilter_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = fromDate.AddDays(7);

        _slots.Setup(r => r.ListByRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<Guid>(), It.IsAny<SlotStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        // Act
        var response = await client.GetAsync(
            $"/api/v1/schedule-slots?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListSlots_WithStatusFilter_ReturnsFilteredSlots()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);

        _slots.Setup(r => r.ListByRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<Guid>(), SlotStatus.Open,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScheduleSlot>());

        // Act
        var response = await client.GetAsync("/api/v1/schedule-slots?status=Open");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _slots.Verify(r => r.ListByRangeAsync(
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<Guid>(), SlotStatus.Open,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Close Slot Tests

    [Fact]
    public async Task CloseSlot_NoBookings_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = GetDoctorId(),
            SlotDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Open,
            Appointments = new List<Appointment>(),
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.GetByIdForUpdateAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/close", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CloseSlotImpactResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal(0, body.Data!.AffectedBookingsCount);
    }

    [Fact]
    public async Task CloseSlot_HasBookingsWithoutForce_ReturnsConflict()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = GetDoctorId(),
            SlotDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Open,
            Appointments = new List<Appointment>
            {
                new()
                {
                    AppointmentId = Guid.NewGuid(),
                    SlotId = slotId,
                    PatientProfileId = Guid.NewGuid(),
                    Status = AppointmentStatus.Booked,
                }
            },
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.GetByIdForUpdateAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/close", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CloseSlotImpactResponse>>();
        Assert.Equal(409, body!.Code);
        Assert.Equal(1, body.Data!.AffectedBookingsCount);
    }

    [Fact]
    public async Task CloseSlot_WithForce_ClosesSlot()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = GetDoctorId(),
            SlotDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Open,
            Appointments = new List<Appointment>
            {
                new()
                {
                    AppointmentId = Guid.NewGuid(),
                    SlotId = slotId,
                    PatientProfileId = Guid.NewGuid(),
                    Status = AppointmentStatus.Booked,
                }
            },
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.GetByIdForUpdateAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/close?force=true", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CloseSlotImpactResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal(SlotStatus.Closed, slot.Status);
    }

    [Fact]
    public async Task CloseSlot_AlreadyClosed_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = GetDoctorId(),
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Closed,
            Appointments = new List<Appointment>(),
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.GetByIdForUpdateAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/close", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Reopen Slot Tests

    [Fact]
    public async Task ReopenSlot_ClosedSlot_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = GetDoctorId(),
            SlotDate = futureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Closed,
            Appointments = new List<Appointment>(),
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.GetByIdForUpdateAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.UpdateAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/reopen", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScheduleSlotResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal(SlotStatus.Open, body.Data!.Status);
    }

    [Fact]
    public async Task ReopenSlot_AlreadyOpen_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = GetDoctorId(),
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Open,
            Appointments = new List<Appointment>(),
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        _slots.Setup(r => r.GetByIdForUpdateAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/reopen", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReopenSlot_NotOwner_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var slotId = Guid.NewGuid();

        var slot = new ScheduleSlot
        {
            SlotId = slotId,
            DoctorId = Guid.NewGuid(), // Different doctor
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Status = SlotStatus.Closed,
            Appointments = new List<Appointment>(),
        };

        _slots.Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        // Act
        var response = await client.PutAsync($"/api/v1/schedule-slots/{slotId}/reopen", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Ensure Default Slots Tests

    [Fact]
    public async Task EnsureDefaultSlots_ValidMonday_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var nextMonday = GetNextMonday();

        _slots.Setup(r => r.HasOverlapAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _slots.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleSlot s, CancellationToken _) => s);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/schedule-slots/ensure-default?weekStart={nextMonday:yyyy-MM-dd}", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _slots.Verify(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureDefaultSlots_NotMonday_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var notMonday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)); // Ensure it's not Monday

        // Act
        var response = await client.PostAsync(
            $"/api/v1/schedule-slots/ensure-default?weekStart={notMonday:yyyy-MM-dd}", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IScheduleSlotRepository>();
                services.AddScoped(_ => _slots.Object);
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    private Guid _doctorId = Guid.NewGuid();

    private Guid GetDoctorId() => _doctorId;

    private HttpClient CreateDoctorClient(WebApplicationFactory<Program> app)
    {
        var doctor = new User
        {
            UserId = _doctorId,
            Phone = "0900000001",
            FullName = "Dr. Test",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
        };

        _users.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(doctor);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreatePatientClient(WebApplicationFactory<Program> app)
    {
        var patientId = Guid.NewGuid();
        var patient = new User
        {
            UserId = patientId,
            Phone = "0900000002",
            FullName = "Patient Test",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
        };

        _users.Setup(r => r.GetByIdAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(patient);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateNurseClient(WebApplicationFactory<Program> app)
    {
        var nurseId = Guid.NewGuid();
        var nurse = new User
        {
            UserId = nurseId,
            Phone = "0900000003",
            FullName = "Nurse Test",
            PasswordHash = "hash",
            Role = UserRole.Nurse,
            Status = UserStatus.Active,
        };

        _users.Setup(r => r.GetByIdAsync(nurseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nurse);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(nurse);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static DateOnly GetNextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        return today.AddDays(daysUntilMonday);
    }

    #endregion
}
