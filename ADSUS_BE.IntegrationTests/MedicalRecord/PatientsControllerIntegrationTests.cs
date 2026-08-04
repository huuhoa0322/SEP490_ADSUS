using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.MedicalRecord;

/// <summary>UC-09 — #26.</summary>
public class PatientsControllerIntegrationTests
{
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly User _doctor = new()
    {
        UserId = Guid.NewGuid(), FullName = "BS. Lê Minh Hoàng", Phone = "0913456789",
        PasswordHash = "x", Role = UserRole.Doctor, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _patientUser = new()
    {
        UserId = Guid.NewGuid(), FullName = "Nguyễn Thị Hoa", Phone = "0981111001",
        PasswordHash = "x", Role = UserRole.Patient, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task GetPatients_ValidQuery_Returns200OkWithPagedResult()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        _profiles.Setup(r => r.SearchAsync(null, null, 1, 20, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((new List<(PatientProfile, Case?)>(), 0));

        // Act
        var response = await client.GetAsync("/api/v1/patients");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<PatientSummaryResponse>>>();
        Assert.Equal(200, body!.Code);
        Assert.Empty(body.Data!.Items);
    }

    [Fact]
    public async Task GetPatients_InvalidVisitStatus_Returns400BadRequest()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        // Act
        var response = await client.GetAsync("/api/v1/patients?visitStatus=Bogus");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPatients_NoToken_Returns401Unauthorized()
    {
        // Arrange
        using var app = MakeApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/patients");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatients_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange — PatientsController giới hạn [Authorize(Roles = "DOCTOR,NURSE")].
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);

        // Act
        var response = await client.GetAsync("/api/v1/patients");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- helpers ----

    private WebApplicationFactory<Program> MakeApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPatientProfileRepository>();
                services.AddScoped(_ => _profiles.Object);
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });

    private HttpClient MakeClientWithToken(WebApplicationFactory<Program> app, User caller)
    {
        // Pipeline xác thực gọi IUserRepository.GetByIdAsync(callerId) để kiểm tra tài khoản
        // chưa bị khoá (AccountStatusJwtEvents) — PHẢI mock caller ở đây (bài học từ Task 10).
        _users.Setup(r => r.GetByIdAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(caller);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
