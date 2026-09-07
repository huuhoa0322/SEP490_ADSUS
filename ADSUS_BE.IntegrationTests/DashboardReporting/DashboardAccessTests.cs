using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.DashboardReporting;

/// <summary>
/// UC-05 BR-03 — chỉ Admin xem được màn thống kê.
///
/// Bảng quyền PRD §3.2: "Statistics dashboard | View" là Full cho Admin, No cho Doctor/Nurse
/// và Patient. Chốt chặn nằm ở [Authorize(Roles = "ADMIN")] nên phải kiểm qua HTTP thật.
/// </summary>
public class DashboardAccessTests
{
    private const string StatisticsPath = "/api/v1/dashboard/statistics";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDashboardRepository> _dashboard = new();
    private readonly Mock<IAiModelVersionRepository> _aiModelVersions = new();

    [Theory]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Nurse)]
    [InlineData(UserRole.Patient)]
    public async Task NonAdminRole_IsForbidden(UserRole role)
    {
        using var app = CreateApp();
        var client = CreateClient(app, role);

        var response = await client.GetAsync(StatisticsPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanViewStatistics()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        var response = await client.GetAsync(StatisticsPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmptyTimeRange_StillReturns200_DoesNotBreak()
    {
        // AF-01 — khoảng thời gian không có hoạt động nào thì hiện toàn số 0, không báo lỗi.
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        var response = await client.GetAsync($"{StatisticsPath}?fromDate=2000-01-01&toDate=2000-01-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ViewsStatistics_RealNumbersFlowThroughFullHttpPipeline()
    {
        // Khác các test trên (RBAC-only, toàn số 0): test này nạp dữ liệu thật khác 0 để xác
        // nhận Controller → Service → JSON serialize/deserialize không làm sai lệch số liệu.
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        _dashboard.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new AccountCounts(
                      Total: 10, AdminCount: 1, DoctorCount: 3, NurseCount: 2, PatientCount: 4,
                      ActiveCount: 9, DeactivatedCount: 1));
        _dashboard.Setup(r => r.GetActivityCountsAsync(
                      It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ActivityCounts(
                      NewAccounts: 2, CaseCount: 5, AiRunCount: 0, AiConfirmedCount: 0,
                      AiRejectedCount: 0, AiPendingCount: 0,
                      AppointmentBookedCount: 6, AppointmentCancelledCount: 4,
                      ScheduleSlotCount: 8, MedicationDoseCount: 20, MedicationTakenCount: 15));

        var response = await client.GetAsync(StatisticsPath);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardStatisticsResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body!.Data);
        Assert.Equal(10, body.Data!.Accounts.Total);
        Assert.Equal(90.0, body.Data.Accounts.ActiveRate);
        Assert.Equal(5, body.Data.Clinical.CaseCount);
        Assert.Equal(40.0, body.Data.Appointments.CancellationRate);
        Assert.Equal(75.0, body.Data.Adherence.AdherenceRate);
    }

    [Fact]
    public async Task MalformedDates_StillReturns200()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        var response = await client.GetAsync($"{StatisticsPath}?fromDate=hom-qua&toDate=!!!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- helpers ----

    private WebApplicationFactory<Program> CreateApp()
    {
        _dashboard.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new AccountCounts(0, 0, 0, 0, 0, 0, 0));
        _dashboard.Setup(r => r.GetActivityCountsAsync(
                      It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ActivityCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        _dashboard.Setup(r => r.GetDailyActivityAsync(
                      It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<DailyActivity>());
        _aiModelVersions.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync((AiModelVersion?)null);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
                services.RemoveAll<IDashboardRepository>();
                services.AddScoped(_ => _dashboard.Object);
                services.RemoveAll<IAiModelVersionRepository>();
                services.AddScoped(_ => _aiModelVersions.Object);
            });
        });
    }

    private HttpClient CreateClient(WebApplicationFactory<Program> app, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000001",
            FullName = "Người kiểm thử",
            PasswordHash = "khong-dung-toi",
            Role = role,
            Status = UserStatus.Active,
        };

        // AccountStatusJwtEvents đọc lại tài khoản ở mỗi request.
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
