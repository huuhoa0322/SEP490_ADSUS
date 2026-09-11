using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.AppointmentScheduling;

/// <summary>
/// RBAC Integration tests for Appointment Reschedule endpoints:
/// - POST /api/v1/appointments/{id}/reschedule
/// - GET /api/v1/appointments/available-slots
/// Verifies NURSE and ADMIN access (201 / 200), forbidden roles PATIENT, DOCTOR, PHARMACIST (403), and missing token (401).
/// </summary>
public class AppointmentsControllerRescheduleIntegrationTests
{
    private const string BaseReschedulePath = "/api/v1/appointments";
    private const string AvailableSlotsPath = "/api/v1/appointments/available-slots";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAppointmentService> _appointmentService = new();

    #region POST /api/v1/appointments/{id}/reschedule RBAC Tests

    [Theory]
    [InlineData(UserRole.Staff)]
    [InlineData(UserRole.Admin)]
    public async Task RescheduleAppointment_AuthorizedRoles_ReturnsCreated(UserRole role)
    {
        using var app = CreateApp();
        var client = CreateClient(app, role);
        var appointmentId = Guid.NewGuid();

        _appointmentService.Setup(s => s.RescheduleAppointmentAsync(
            It.IsAny<Guid>(),
            It.IsAny<RescheduleAppointmentRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppointmentResponse
            {
                AppointmentId = Guid.NewGuid(),
                ScheduleSlotId = Guid.NewGuid(),
                SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                DoctorName = "BS Test",
                Status = AppointmentStatus.Booked,
                Reason = "Test Reschedule",
            });

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Bác sĩ bận việc đột xuất",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Pharmacist)]
    public async Task RescheduleAppointment_UnauthorizedRoles_IsForbidden(UserRole role)
    {
        using var app = CreateApp();
        var client = CreateClient(app, role);
        var appointmentId = Guid.NewGuid();

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Yêu cầu từ vai trò không được phép",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_NoToken_IsUnauthorized()
    {
        using var app = CreateApp();
        var client = app.CreateClient();
        var appointmentId = Guid.NewGuid();

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Không gửi token JWT",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /api/v1/appointments/available-slots RBAC Tests

    [Theory]
    [InlineData(UserRole.Staff)]
    [InlineData(UserRole.Admin)]
    public async Task GetAvailableSlots_AuthorizedRoles_ReturnsOk(UserRole role)
    {
        using var app = CreateApp();
        var client = CreateClient(app, role);

        _appointmentService.Setup(s => s.ListOpenSlotsAsync(
            It.IsAny<string?>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenSlotResponse>());

        var response = await client.GetAsync(
            $"{AvailableSlotsPath}?fromDate=2026-09-10&toDate=2026-09-11",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Pharmacist)]
    public async Task GetAvailableSlots_UnauthorizedRoles_IsForbidden(UserRole role)
    {
        using var app = CreateApp();
        var client = CreateClient(app, role);

        var response = await client.GetAsync(AvailableSlotsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableSlots_NoToken_IsUnauthorized()
    {
        using var app = CreateApp();
        var client = app.CreateClient();

        var response = await client.GetAsync(AvailableSlotsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Test Fixture Setup

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
                services.RemoveAll<IAppointmentService>();
                services.AddScoped(_ => _appointmentService.Object);
            });
        });
    }

    private HttpClient CreateClient(WebApplicationFactory<Program> app, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000001",
            FullName = "Test User",
            PasswordHash = "hash",
            Role = role,
            Status = UserStatus.Active,
        };

        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        _users.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    #endregion
}
