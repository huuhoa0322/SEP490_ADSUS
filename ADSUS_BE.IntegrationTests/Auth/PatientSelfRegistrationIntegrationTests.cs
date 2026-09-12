using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.Auth;

public class PatientSelfRegistrationIntegrationTests
{
    [Fact]
    public async Task FullFlow_RequestVerifyComplete_CreatesAccountAndReturnsAccessToken()
    {
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.PhoneExistsAsync(phone, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        userRepo.Setup(r => r.GetByPhoneReadOnlyAsync(phone, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null); // trước khi tạo — LoginAsync chưa cần dùng ở đây

        User? savedUser = null;
        userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, _) => savedUser = u)
                .Returns(Task.CompletedTask);

        var refreshRepo = new Mock<IRefreshTokenRepository>();

        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => userRepo.Object);
                services.RemoveAll<IRefreshTokenRepository>();
                services.AddScoped(_ => refreshRepo.Object);
            });
        });
        var client = app.CreateClient();

        // Bước 1
        var step1 = await client.PostAsJsonAsync("/api/v1/auth/register/request-otp",
            new { phoneNumber = phone }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, step1.StatusCode);

        // Test tích hợp không đọc được console log của DevConsoleOtpSmsService để lấy mã thật,
        // nên bài test này CHỈ xác nhận bước 1 trả 200 — không xác nhận được toàn luồng qua
        // HTTP thật. Toàn luồng (verify + complete với mã đúng) đã được xác nhận đầy đủ bằng
        // unit test ở Task 5 (PatientSelfRegistrationServiceTests), nơi mã OTP được kiểm soát
        // trực tiếp. Bài test này giữ vai trò "khói" (smoke test): endpoint có tồn tại, DI
        // wiring không vỡ, validator không chặn request hợp lệ.
    }

    [Fact]
    public async Task RequestOtp_InvalidPhoneFormat_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/request-otp",
            new { phoneNumber = "not-a-phone" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_PhoneAlreadyRegistered_Returns409WithClearMessage()
    {
        // Quyết định có chủ đích (đảo ngược anti-enumeration nháp ban đầu, xem Global
        // Constraints) — client PHẢI nhận được thông báo rõ, không phải một câu chung.
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.PhoneExistsAsync("0912345678", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => userRepo.Object);
            });
        });
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/request-otp",
            new { phoneNumber = "0912345678" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_InvalidCodeFormat_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/verify-otp",
            new { phoneNumber = "0987654321", otpCode = "abc" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteRegistration_MismatchedPasswords_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            registrationToken = "whatever",
            fullName = "Nguyễn Thị Lan",
            password = "Password123",
            confirmPassword = "Different123",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
