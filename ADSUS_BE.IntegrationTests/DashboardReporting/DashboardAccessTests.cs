using System.Net;
using System.Net.Http.Headers;
using ADSUS_BE.BLL.Auth.Interfaces;
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
    private const string DuongDan = "/api/v1/dashboard/statistics";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDashboardRepository> _dashboard = new();

    [Theory]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Nurse)]
    [InlineData(UserRole.Patient)]
    public async Task VaiTroKhongPhaiAdmin_BiTuChoi(UserRole vaiTro)
    {
        using var app = TaoApp();
        var client = TaoClient(app, vaiTro);

        var response = await client.GetAsync(DuongDan);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_XemDuoc()
    {
        using var app = TaoApp();
        var client = TaoClient(app, UserRole.Admin);

        var response = await client.GetAsync(DuongDan);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task KhongCoDuLieu_VanTraVe200_KhongVo()
    {
        // AF-01 — khoảng thời gian không có hoạt động nào thì hiện toàn số 0, không báo lỗi.
        using var app = TaoApp();
        var client = TaoClient(app, UserRole.Admin);

        var response = await client.GetAsync($"{DuongDan}?fromDate=2000-01-01&toDate=2000-01-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NgayThangSaiDinhDang_VanTraVe200()
    {
        using var app = TaoApp();
        var client = TaoClient(app, UserRole.Admin);

        var response = await client.GetAsync($"{DuongDan}?fromDate=hom-qua&toDate=!!!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- helpers ----

    private WebApplicationFactory<Program> TaoApp()
    {
        _dashboard.Setup(r => r.GetAccountCountsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new AccountCounts(0, 0, 0, 0, 0, 0, 0, 0));
        _dashboard.Setup(r => r.GetActivityCountsAsync(
                      It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new ActivityCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        _dashboard.Setup(r => r.GetDailyActivityAsync(
                      It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Array.Empty<DailyActivity>());

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
                services.RemoveAll<IDashboardRepository>();
                services.AddScoped(_ => _dashboard.Object);
            });
        });
    }

    private HttpClient TaoClient(WebApplicationFactory<Program> app, UserRole vaiTro)
    {
        var nguoiDung = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000001",
            FullName = "Người kiểm thử",
            PasswordHash = "khong-dung-toi",
            Role = vaiTro,
            Status = UserStatus.Active,
        };

        // AccountStatusJwtEvents đọc lại tài khoản ở mỗi request.
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(nguoiDung);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(nguoiDung);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
