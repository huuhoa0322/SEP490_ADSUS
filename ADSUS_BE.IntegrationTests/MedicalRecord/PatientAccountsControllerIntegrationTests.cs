using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.MedicalRecord;

/// <summary>
/// UC-06 AF-01/02/03 — 3 endpoint CHỈ dành cho Điều dưỡng.
///
/// Đây là ngoại lệ đầu tiên trong bộ quyền vốn giống hệt nhau giữa Bác sĩ và Điều dưỡng, nên
/// trường hợp "Bác sĩ bị 403" là kịch bản nghiệp vụ chính, không phải test RBAC lấy lệ.
/// </summary>
public class PatientAccountsControllerIntegrationTests
{
    private readonly Mock<IPatientAccountService> _accounts = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly User _nurse = MakeUser(UserRole.Nurse, "ĐD. Võ Thị Thu Hà");
    private readonly User _doctor = MakeUser(UserRole.Doctor, "BS. Lê Minh Hoàng");

    private static User MakeUser(UserRole role, string fullName) => new()
    {
        UserId = Guid.NewGuid(),
        FullName = fullName,
        Phone = "09" + Random.Shared.Next(10000000, 99999999),
        Role = role,
        Status = UserStatus.Active,
        PasswordHash = "x",
    };

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPatientAccountService>();
                services.AddScoped(_ => _accounts.Object);
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            }));

    private HttpClient MakeClientWithToken(WebApplicationFactory<Program> app, User caller)
    {
        _users.Setup(r => r.GetByIdAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(caller);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static CreatePatientAccountRequest ValidCreateBody() => new(
        PhoneNumber: "0981234567",
        FullName: "Lê Thị Hoa",
        DateOfBirth: new DateOnly(1984, 3, 12),
        Email: "hoa@example.com");

    private static UpdatePatientAccountRequest ValidUpdateBody() => new(
        FullName: "Lê Thị Hoà",
        PhoneNumber: "0981234567",
        DateOfBirth: new DateOnly(1984, 3, 12),
        Email: "hoa@example.com");

    [Fact]
    public async Task PostPatientAccount_CalledByNurse_Returns201()
    {
        // Arrange
        var created = new PatientAccountCreatedResponse(
            Guid.NewGuid(), "Lê Thị Hoa", "0981234567", new DateOnly(1984, 3, 12), "hoa@example.com",
            TemporaryPassword: "Ab3xyz9pqr");
        _accounts.Setup(s => s.CreateAsync(
                     It.IsAny<CreatePatientAccountRequest>(), _nurse.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(created);

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _nurse);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patients", ValidCreateBody());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // ApiResponse.Ok hard-code Code = 200 bất kể HTTP status thật — quy ước toàn repo.
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PatientAccountCreatedResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal(new DateOnly(1984, 3, 12), body.Data!.DateOfBirth);
        Assert.Equal("Ab3xyz9pqr", body.Data!.TemporaryPassword);
    }

    [Fact]
    public async Task PostPatientAccount_CalledByDoctor_Returns403Forbidden()
    {
        // Arrange — BR-03: đây là ngoại lệ đầu tiên Nurse có quyền mà Doctor không có.
        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _doctor);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patients", ValidCreateBody());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _accounts.Verify(s => s.CreateAsync(
            It.IsAny<CreatePatientAccountRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PutPatientAccount_CalledByDoctor_Returns403Forbidden()
    {
        // Arrange
        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _doctor);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/patients/{Guid.NewGuid()}", ValidUpdateBody());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutResetPassword_CalledByNurse_Returns200()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        _accounts.Setup(s => s.ResetPasswordAsync(targetId, _nurse.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((string?)null);

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _nurse);

        // Act
        var response = await client.PutAsync($"/api/v1/patients/{targetId}/reset-password", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _accounts.Verify(s => s.ResetPasswordAsync(
            targetId, _nurse.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutResetPassword_CalledByDoctor_Returns403Forbidden()
    {
        // Arrange
        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _doctor);

        // Act
        var response = await client.PutAsync($"/api/v1/patients/{Guid.NewGuid()}/reset-password", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostPatientAccount_InvalidPhoneFormat_Returns400()
    {
        // Arrange — validator chặn trước khi chạm tới service.
        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _nurse);
        var body = ValidCreateBody() with { PhoneNumber = "123" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patients", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _accounts.Verify(s => s.CreateAsync(
            It.IsAny<CreatePatientAccountRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
