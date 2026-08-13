using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.AppointmentScheduling;

public class CreateOvertimeSlotsTests
{
    private const string ApiUrl = "/api/v1/schedule-slots/overtime";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IScheduleSlotRepository> _slots = new();
    private readonly User _doctor = new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0912345678",
        FullName = "Dr. Test",
        PasswordHash = "khong-dung",
        Role = UserRole.Doctor,
        Status = UserStatus.Active,
    };

    [Fact]
    public async Task CreateOvertimeSlots_DoctorRole_ReturnsOk()
    {
        using var app = TaoApp();
        var client = TaoClientCoToken(app, _doctor);

        _slots.Setup(r => r.ListByRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<ScheduleSlot>());

        var request = new CreateOvertimeSlotsRequest { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };
        var response = await client.PostAsJsonAsync(ApiUrl, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateOvertimeSlots_PatientRole_ReturnsForbidden()
    {
        using var app = TaoApp();
        var patient = new User { UserId = Guid.NewGuid(), Phone = "0999999999", Role = UserRole.Patient, Status = UserStatus.Active };
        var client = TaoClientCoToken(app, patient);

        var request = new CreateOvertimeSlotsRequest { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };
        var response = await client.PostAsJsonAsync(ApiUrl, request);

        // Authorize(Roles = "Doctor") should reject this
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateOvertimeSlots_Unauthenticated_ReturnsUnauthorized()
    {
        using var app = TaoApp();
        var client = app.CreateClient(); // No token

        var request = new CreateOvertimeSlotsRequest { VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };
        var response = await client.PostAsJsonAsync(ApiUrl, request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- helpers ----

    private WebApplicationFactory<Program> TaoApp()
    {
        _users.Setup(r => r.GetByIdAsync(_doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_doctor);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);

                services.RemoveAll<IScheduleSlotRepository>();
                services.AddScoped(_ => _slots.Object);
            });
        });
    }

    private HttpClient TaoClientCoToken(WebApplicationFactory<Program> app, User user)
    {
        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
