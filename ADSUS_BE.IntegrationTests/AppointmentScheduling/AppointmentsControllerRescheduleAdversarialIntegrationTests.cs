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
/// Adversarial integration tests authored by Challenger 1.
/// Tests error status code mapping (400, 404) and exception translations on controller endpoints.
/// </summary>
public class AppointmentsControllerRescheduleAdversarialIntegrationTests
{
    private const string BaseReschedulePath = "/api/v1/appointments";
    private const string AvailableSlotsPath = "/api/v1/appointments/available-slots";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAppointmentService> _appointmentService = new();

    [Fact]
    public async Task RescheduleAppointment_BusinessRuleViolation_ReturnsBadRequest()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Nurse);
        var appointmentId = Guid.NewGuid();

        _appointmentService.Setup(s => s.RescheduleAppointmentAsync(
            It.IsAny<Guid>(),
            It.IsAny<RescheduleAppointmentRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Ca khám đã kết thúc, không thể đổi lịch."));

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Lý do bất kỳ",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_AppointmentNotFound_ReturnsNotFound()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Nurse);
        var appointmentId = Guid.NewGuid();

        _appointmentService.Setup(s => s.RescheduleAppointmentAsync(
            It.IsAny<Guid>(),
            It.IsAny<RescheduleAppointmentRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Appointment '{appointmentId}' not found."));

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Lý do hợp lệ",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_EmptyReasonArgumentException_ReturnsBadRequest()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Nurse);
        var appointmentId = Guid.NewGuid();

        _appointmentService.Setup(s => s.RescheduleAppointmentAsync(
            It.IsAny<Guid>(),
            It.IsAny<RescheduleAppointmentRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Lý do đổi lịch không được để trống."));

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_SlotNotOpen_ReturnsBadRequest()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Nurse);
        var appointmentId = Guid.NewGuid();

        _appointmentService.Setup(s => s.RescheduleAppointmentAsync(
            It.IsAny<Guid>(),
            It.IsAny<RescheduleAppointmentRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Khung giờ này không còn nhận đặt lịch."));

        var requestBody = new RescheduleAppointmentRequest
        {
            NewScheduleSlotId = Guid.NewGuid(),
            RescheduleReason = "Đổi sang slot đóng",
            AutoCheckin = false,
        };

        var response = await client.PostAsJsonAsync(
            $"{BaseReschedulePath}/{appointmentId}/reschedule",
            requestBody,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

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
}
