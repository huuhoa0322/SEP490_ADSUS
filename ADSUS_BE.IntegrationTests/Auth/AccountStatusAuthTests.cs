using System.Net;
using System.Net.Http.Headers;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.Auth;

/// <summary>
/// Token còn hạn nhưng tài khoản đã bị vô hiệu hoá thì phải bị chặn NGAY.
///
/// Không kiểm được bằng unit test: chỗ chặn nằm trong đường ống xác thực của ASP.NET
/// (<c>AccountStatusJwtEvents</c>), chỉ chạy khi có một request HTTP thật đi qua.
///
/// Vì sao quan trọng: JWT tự chứng minh, backend không giữ danh sách token đã phát. Nếu
/// không có lớp chặn này thì Admin vô hiệu hoá tài khoản (UC-04 / FT-08) mà token cũ vẫn
/// gọi API được cho tới lúc hết hạn — và UC-02 AF-02 (vân tay đúng nhưng tài khoản đã bị
/// vô hiệu hoá) cũng không thể thực hiện được.
///
/// Bài test dựng app thật nhưng THAY repository bằng bản giả, nên không đụng gì tới
/// database của nhóm.
/// </summary>
public class AccountStatusAuthTests
{
    private const string DuongDanHoSo = "/api/v1/users/me";

    private readonly Mock<IUserRepository> _users = new();
    private readonly User _taiKhoan = new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0912345678",
        FullName = "Nguyễn Văn A",
        PasswordHash = "khong-dung-toi-trong-bai-test-nay",
        Role = UserRole.Patient,
        Status = UserStatus.Active,
    };

    [Fact]
    public async Task TaiKhoanConHieuLuc_GoiDuocApi()
    {
        using var app = TaoApp();
        var client = TaoClientCoToken(app);

        var response = await client.GetAsync(DuongDanHoSo);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(UserStatus.Deactivated)]
    public async Task TokenConHan_NhungTaiKhoanBiKhoa_TraVe401(UserStatus trangThai)
    {
        using var app = TaoApp();

        // Token được phát lúc tài khoản còn hiệu lực...
        var client = TaoClientCoToken(app);

        // ...rồi Admin vô hiệu hoá tài khoản. Token trong tay người dùng KHÔNG đổi.
        _taiKhoan.Status = trangThai;

        var response = await client.GetAsync(DuongDanHoSo);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TaiKhoanBiXoa_TraVe401()
    {
        using var app = TaoApp();
        var client = TaoClientCoToken(app);

        // Cả AccountStatusJwtEvents (chặn ở tầng xác thực) lẫn ProfileService.GetOwnProfileAsync
        _users.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var response = await client.GetAsync(DuongDanHoSo);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- helpers ----

    /// <summary>
    /// Dựng ứng dụng thật, chỉ tráo <see cref="IUserRepository"/> sang bản giả.
    /// Nhờ vậy không có lời gọi nào chạm tới PostgreSQL.
    /// </summary>
    private WebApplicationFactory<Program> TaoApp()
    {
        // Cả AccountStatusJwtEvents (chặn ở tầng xác thực) lẫn ProfileService.GetOwnProfileAsync
        _users.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => _taiKhoan);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    /// <summary>Phát token đúng như lúc đăng nhập thành công, rồi gắn vào header.</summary>
    private HttpClient TaoClientCoToken(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(_taiKhoan);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
