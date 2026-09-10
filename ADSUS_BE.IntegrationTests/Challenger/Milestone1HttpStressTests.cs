using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.Challenger;

/// <summary>
/// HTTP Integration-level adversarial stress tests for Milestone 1 endpoints.
/// Verifies that the ASP.NET Core pipeline handles boundary inputs, inverted dates,
/// injection attacks, and negative pagination without 500 exceptions or crashes.
/// </summary>
public class Milestone1HttpStressTests
{
    private const string CheckinQueuePath = "/api/v1/appointments/checkin-queue";
    private const string AuditLogsPath = "/api/v1/admin/audit-logs";
    private const string AdminFeedbacksPath = "/api/v1/admin/feedbacks";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAppointmentService> _appointmentService = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly Mock<IFeedbackRepository> _feedbacks = new();
    private readonly Mock<IPatientProfileRepository> _patientProfiles = new();

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

                services.RemoveAll<IAuditLogRepository>();
                services.AddScoped(_ => _auditLogs.Object);

                services.RemoveAll<IFeedbackRepository>();
                services.AddScoped(_ => _feedbacks.Object);

                services.RemoveAll<IPatientProfileRepository>();
                services.AddScoped(_ => _patientProfiles.Object);
            });
        });
    }

    private HttpClient CreateClient(WebApplicationFactory<Program> app, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000001",
            FullName = "Stress Challenger",
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

    [Fact]
    public async Task CheckinQueue_InvertedDatesAndMaliciousSearch_Returns200AndValidEnvelope()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Nurse);

        _appointmentService.Setup(s => s.GetCheckinQueueAsync(
            It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckinQueueResponse
            {
                Page = 1,
                PageSize = 15,
                TotalCount = 0,
                Items = new List<CheckinQueueItemResponse>(),
            });

        var url = $"{CheckinQueuePath}?fromDate=2026-12-31&toDate=2026-01-01&search=%27%20OR%201=1;--&status=%3Cscript%3E&page=-1&pageSize=0";
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckinQueueResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(200, body.Code);
        Assert.Equal(1, body.Data?.Page);
    }

    [Fact]
    public async Task AuditLogs_BoundaryParametersAndInjections_Returns200AndValidEnvelope()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        _auditLogs.Setup(r => r.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogEntry>(), 0));

        var url = $"{AuditLogsPath}?fromDate=2026-12-31T00:00:00Z&toDate=2026-01-01T00:00:00Z&search=%27%20OR%201=1;--&action=%3Cscript%3E&role=HACKER&page=0&pageSize=9999";
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(200, body.Code);
        Assert.Equal(1, body.Data?.Page);
    }

    [Fact]
    public async Task AdminFeedbacks_BoundaryRatingsAndNegativePagination_Returns200AndValidEnvelope()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        _feedbacks.Setup(r => r.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<short?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ServiceFeedback>(), 0));

        var url = $"{AdminFeedbacksPath}?search=%27%20OR%201=1;--&minRating=-5&page=-10&pageSize=-20";
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<FeedbackResponse>>>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(200, body.Code);
        Assert.Equal(1, body.Data?.Page);
    }
}
