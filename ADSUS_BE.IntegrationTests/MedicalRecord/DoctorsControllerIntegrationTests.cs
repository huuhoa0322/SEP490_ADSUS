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

public class DoctorsControllerIntegrationTests
{
    private readonly Mock<IDoctorDirectoryService> _doctors = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly User _doctorCaller = MakeUser(UserRole.Doctor, "BS. Lê Minh Hoàng");
    private readonly User _nurseCaller = MakeUser(UserRole.Nurse, "ĐD. Võ Thị Thu Hà");
    private readonly User _patientCaller = MakeUser(UserRole.Patient, "Trần Thị Mai");

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
                services.RemoveAll<IDoctorDirectoryService>();
                services.AddScoped(_ => _doctors.Object);
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            }));

    /// <summary>
    /// Pipeline xác thực gọi IUserRepository.GetByIdAsync ở MỌI request có token, để kiểm
    /// tài khoản có bị khoá không. Không mock người gọi thì mọi test đều nhận 401.
    /// </summary>
    private HttpClient MakeClientWithToken(WebApplicationFactory<Program> app, User caller)
    {
        _users.Setup(r => r.GetByIdAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);

        _users.Setup(r => r.GetByIdReadOnlyAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);
        _users.Setup(r => r.GetByIdReadOnlyAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(caller);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetDoctors_CalledByDoctor_Returns200WithList()
    {
        // Arrange
        _doctors.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DoctorSummaryResponse>
                {
                    new(_doctorCaller.UserId, _doctorCaller.FullName),
                });

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _doctorCaller);

        // Act
        var response = await client.GetAsync("/api/v1/doctors");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<DoctorSummaryResponse>>>();
        Assert.Equal(_doctorCaller.FullName, body!.Data!.Single().FullName);
    }

    [Fact]
    public async Task GetDoctors_CalledByNurse_Returns200()
    {
        // Arrange — UC-07 cho Điều dưỡng tạo ca hộ Bác sĩ, nên phải chọn được người phụ trách.
        _doctors.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DoctorSummaryResponse>());

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _nurseCaller);

        // Act
        var response = await client.GetAsync("/api/v1/doctors");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDoctors_CalledByPatient_Returns403Forbidden()
    {
        // Arrange — bệnh nhân không có việc gì với danh sách nhân sự phòng khám.
        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _patientCaller);

        // Act
        var response = await client.GetAsync("/api/v1/doctors");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
