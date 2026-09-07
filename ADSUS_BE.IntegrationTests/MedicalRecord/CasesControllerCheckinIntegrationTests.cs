using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.MedicalRecord;

/// <summary>
/// Integration tests cho CasesController.CheckinAppointment endpoint (Nurse Checkin).
/// Role: NURSE, ADMIN
/// Flow: Booked → Approved
///
/// NOTE: Business logic (status transitions) được test kỹ trong
/// AppointmentServiceCheckinTests (Unit Tests). Integration tests này tập trung vào:
/// - Authorization (role-based access control)
/// - HTTP response codes
/// </summary>
public class CasesControllerCheckinIntegrationTests
{
    private readonly Mock<IUserRepository> _users = new();

    #region Test Data

    private readonly User _doctor = new()
    {
        UserId = Guid.NewGuid(),
        FullName = "Dr. Test",
        Phone = "0900000001",
        PasswordHash = "hash",
        Role = UserRole.Doctor,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _nurse = new()
    {
        UserId = Guid.NewGuid(),
        FullName = "Nurse Test",
        Phone = "0900000003",
        PasswordHash = "hash",
        Role = UserRole.Nurse,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _admin = new()
    {
        UserId = Guid.NewGuid(),
        FullName = "Admin Test",
        Phone = "0900000000",
        PasswordHash = "hash",
        Role = UserRole.Admin,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _patient = new()
    {
        UserId = Guid.NewGuid(),
        FullName = "Patient Test",
        Phone = "0900000002",
        PasswordHash = "hash",
        Role = UserRole.Patient,
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    #endregion

    #region TC-INT-Checkin-001: Authorization - Patient Cannot Checkin

    [Fact]
    public async Task CheckinAppointment_PatientRole_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateClientWithToken(app, _patient);

        var caseId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region TC-INT-Checkin-002: Authorization - Doctor Cannot Checkin

    [Fact]
    public async Task CheckinAppointment_DoctorRole_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateClientWithToken(app, _doctor);

        var caseId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region TC-INT-Checkin-003: Authorization - Nurse Can Access Endpoint

    [Fact]
    public async Task CheckinAppointment_NurseRole_ReturnsNotFound_OrBadRequest()
    {
        // Arrange - Nurse có quyền gọi endpoint, nhưng case không tồn tại → 404 hoặc 400
        // (tùy implementation)
        using var app = CreateApp();
        var client = CreateClientWithToken(app, _nurse);

        var caseId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null);

        // Assert - Nurse được phép gọi, nhưng case không tồn tại
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 404 or 400, got {response.StatusCode}");
    }

    #endregion

    #region TC-INT-Checkin-004: Authorization - Admin Cannot Access Endpoint

    [Fact]
    public async Task CheckinAppointment_AdminRole_ReturnsForbidden()
    {
        // Arrange - Admin KHÔNG có quyền gọi endpoint (chỉ NURSE được phép)
        using var app = CreateApp();
        var client = CreateClientWithToken(app, _admin);

        var caseId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null);

        // Assert - Admin bị cấm, không có quyền checkin
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region TC-INT-Checkin-005: Unauthorized Access

    [Fact]
    public async Task CheckinAppointment_NoToken_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // Không có token

        var caseId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Mock UserRepository để authenticate
                    services.RemoveAll(typeof(IUserRepository));
                    services.AddScoped(_ => _users.Object);
                });
            });

    private HttpClient CreateClientWithToken(WebApplicationFactory<Program> app, User caller)
    {
        // Pipeline xác thực gọi IUserRepository.GetByIdAsync(callerId)
        _users.Setup(r => r.GetByIdReadOnlyAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(caller);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    #endregion
}
