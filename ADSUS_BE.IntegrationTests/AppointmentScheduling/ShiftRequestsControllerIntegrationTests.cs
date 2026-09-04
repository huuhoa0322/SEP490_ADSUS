using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.AppointmentScheduling;

public class ShiftRequestsControllerIntegrationTests
{
    private readonly Mock<IShiftRequestRepository> _shiftRequests = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    [Fact]
    public async Task CreateRequest_ValidInput_Returns201Created()
    {
        using var app = CreateApp();
        var client = CreateDoctorClient(app);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        _shiftRequests.Setup(r => r.HasActiveRequestAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<ShiftType>(), It.IsAny<ShiftRequestType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
            
        _shiftRequests.Setup(r => r.AddAsync(It.IsAny<ShiftRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new CreateShiftRequestDto
        {
            RequestType = ShiftRequestType.Leave,
            ShiftType = ShiftType.Afternoon,
            RequestDate = futureDate,
            Reason = "Integration test"
        };

        var response = await client.PostAsJsonAsync("/api/v1/shift-requests", dto);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftRequestResponse>>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        Assert.NotNull(result);
        Assert.Equal(201, result!.Code);
        Assert.NotNull(result.Data);
        Assert.Equal(ShiftRequestStatus.Pending, result.Data!.Status);
    }

    [Fact]
    public async Task ReviewRequest_AdminRejects_Returns200Ok()
    {
        using var app = CreateApp();
        var client = CreateAdminClient(app);
        var requestId = Guid.NewGuid();
        
        var request = new ShiftRequest
        {
            RequestId = requestId,
            UserId = _doctorId,
            RequestType = ShiftRequestType.Overtime,
            ShiftType = ShiftType.Evening,
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Reason = "Need overtime",
            Status = ShiftRequestStatus.Pending
        };

        _shiftRequests.Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _shiftRequests.Setup(r => r.UpdateAsync(It.IsAny<ShiftRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var doctor = new User { UserId = _doctorId, FullName = "Dr. Test" };
        _users.Setup(u => u.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);

        var reviewDto = new ReviewShiftRequestDto
        {
            Decision = "REJECTED",
            RejectReason = "Too many overtimes"
        };

        var reviewResponse = await client.PutAsJsonAsync($"/api/v1/admin/shift-requests/{requestId}/review", reviewDto);
        
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<ApiResponse<ShiftRequestResponse>>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        Assert.Equal(ShiftRequestStatus.Rejected, reviewResult!.Data!.Status);
    }

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IShiftRequestRepository>();
                services.AddScoped(_ => _shiftRequests.Object);
                // Cần stub luôn IScheduleSlotRepository vì ShiftRequestService gọi nó
                var slotsMock = new Mock<IScheduleSlotRepository>();
                slotsMock.Setup(r => r.ListByRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<SlotStatus?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ScheduleSlot>());
                slotsMock.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>())).ReturnsAsync((ScheduleSlot s, CancellationToken ct) => s);
                services.RemoveAll<IScheduleSlotRepository>();
                services.AddScoped(_ => slotsMock.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
                
                var notifMock = new Mock<ADSUS_BE.BLL.Common.Interfaces.INotificationService>();
                notifMock.Setup(n => n.SendAsync(It.IsAny<ADSUS_BE.BLL.Common.Interfaces.SendNotificationRequest>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(Guid.NewGuid());
                notifMock.Setup(n => n.SendBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<ADSUS_BE.BLL.Common.Interfaces.SendNotificationRequest>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);
                services.RemoveAll<ADSUS_BE.BLL.Common.Interfaces.INotificationService>();
                services.AddScoped(_ => notifMock.Object);
            });
        });
    }

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

        _users.Setup(r => r.GetByIdAsync(_doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);
        _users.Setup(r => r.GetByIdReadOnlyAsync(_doctorId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(doctor);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateAdminClient(WebApplicationFactory<Program> app)
    {
        var admin = new User
        {
            UserId = _adminId,
            Phone = "0900000002",
            FullName = "Admin Test",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            Status = UserStatus.Active,
        };

        _users.Setup(r => r.GetByIdAsync(_adminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _users.Setup(r => r.GetByIdReadOnlyAsync(_adminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(admin);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
