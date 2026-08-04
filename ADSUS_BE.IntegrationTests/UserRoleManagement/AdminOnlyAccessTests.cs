using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.UserRoleManagement;

/// <summary>
/// UC-04 — màn quản lý tài khoản chỉ dành cho Admin.
///
/// Tiêu chí kiểm thử của UC-04 ghi rõ: "A Doctor or Patient attempts to access the User List
/// or Create/Edit User Account screens → denied". Bảng quyền PRD §3.2 cũng để Create,
/// Lock/Deactivate và Assign role đều là No cho Doctor/Nurse/Patient.
///
/// Phải kiểm ở mức HTTP thật, vì chốt chặn nằm ở [Authorize(Roles = "ADMIN")] — thuộc tính
/// này chỉ có tác dụng khi request đi qua đường ống của ASP.NET, unit test không chạm tới.
///
/// Điều dưỡng được kiểm riêng: NURSE có quyền y hệt DOCTOR ở mọi màn lâm sàng, nên rất dễ bị
/// hiểu nhầm thành "giống Doctor ở mọi nơi". Quản lý tài khoản là chỗ cả hai đều bị chặn.
/// </summary>
public class AdminOnlyAccessTests
{
    private const string DuongDan = "/api/v1/admin/users";

    private readonly Mock<IUserRepository> _users = new();

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
    public async Task Admin_VaoDuoc()
    {
        using var app = TaoApp();
        var client = TaoClient(app, UserRole.Admin);

        var response = await client.GetAsync(DuongDan);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Nurse)]
    public async Task VaiTroKhongPhaiAdmin_KhongTaoDuocTaiKhoan(UserRole vaiTro)
    {
        using var app = TaoApp();
        var client = TaoClient(app, vaiTro);

        var response = await client.PostAsJsonAsync(DuongDan, new
        {
            phoneNumber = "0988776655",
            fullName = "BS. Trần Văn B",
            role = "DOCTOR",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task KhongCoToken_BiTuChoi()
    {
        using var app = TaoApp();

        var response = await app.CreateClient().GetAsync(DuongDan);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- helpers ----

    /// <summary>Dựng app thật, tráo repository sang bản giả nên không chạm database của nhóm.</summary>
    private WebApplicationFactory<Program> TaoApp()
    {
        _users.Setup(r => r.SearchAsync(
                  It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<UserStatus?>(),
                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Array.Empty<User>(), 0));

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    /// <summary>Phát token cho một vai trò rồi gắn vào header.</summary>
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

        // AccountStatusJwtEvents đọc lại tài khoản từ repository ở mỗi request, nên bản giả
        // phải trả về đúng người dùng này, nếu không mọi request đều nhận 401.
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
