using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using ADSUS_BE.BLL.HealthMonitoring.Validators;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.HealthMonitoring;

/// <summary>
/// Integration tests for HealthLogsController (UC-21, FT-35).
/// Tests HTTP endpoints #55 (POST) and #56 (GET) with mocked repositories.
///
/// Allowed roles: Patient only.
/// Based on API Spec Module09.
///
/// Test cases:
/// - POST /api/v1/health-logs: 14 cases (happy path, validation errors, auth)
/// - GET /api/v1/health-logs: 11 cases (date filtering, empty results, auth)
/// </summary>
public class HealthLogsControllerIntegrationTests
{
    private readonly Mock<IHealthLogRepository> _healthLogs = new();
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly IValidator<LogHealthDataRequest> _validator = new LogHealthDataRequestValidator();

    // Shared patient ID for tests to ensure mock setup matches token generation
    private readonly Guid _patientId = Guid.NewGuid();

    #region Test Data Factory

    private User NewUser(Guid userId, UserRole role)
        => new()
        {
            UserId = userId,
            Phone = "0900000000",
            FullName = $"Test {role}",
            PasswordHash = "hash-not-used-in-tests",
            Role = role,
            Status = UserStatus.Active,
        };

    private PatientProfile NewPatientProfile(Guid userId)
        => new()
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = userId,
            User = NewUser(userId, UserRole.Patient),
            Gender = GenderType.Male,
            CreatedAt = DateTime.UtcNow,
        };

    #endregion

    #region Setup Helpers

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> app,
        Mock<IUserRepository> userRepo,
        UserRole role,
        Guid userId)
    {
        var user = new User
        {
            UserId = userId,
            Phone = "0900000000",
            FullName = $"Test {role}",
            PasswordHash = "hash-not-used-in-tests",
            Role = role,
            Status = UserStatus.Active,
        };

        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        userRepo.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        using var scope = app.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private WebApplicationFactory<Program> CreateApp()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHealthLogRepository>();
                    services.RemoveAll<IPatientProfileRepository>();
                    services.RemoveAll<IUserRepository>();
                    services.RemoveAll<IValidator<LogHealthDataRequest>>();

                    services.AddScoped(_ => _healthLogs.Object);
                    services.AddScoped(_ => _profiles.Object);
                    services.AddScoped(_ => _users.Object);
                    services.AddScoped(_ => _validator);
                });
            });

        return factory;
    }

    private HttpClient CreatePatientClient(WebApplicationFactory<Program> app)
        => CreateAuthenticatedClient(app, _users, UserRole.Patient, _patientId);

    private HttpClient CreateDoctorClient(WebApplicationFactory<Program> app)
        => CreateAuthenticatedClient(app, _users, UserRole.Doctor, Guid.NewGuid());

    private HttpClient CreateNurseClient(WebApplicationFactory<Program> app)
        => CreateAuthenticatedClient(app, _users, UserRole.Nurse, Guid.NewGuid());

    private HttpClient CreateAdminClient(WebApplicationFactory<Program> app)
        => CreateAuthenticatedClient(app, _users, UserRole.Admin, Guid.NewGuid());

    #endregion

    #region POST /api/v1/health-logs Tests

    [Fact]
    public async Task PostHealthLog_ValidExercise_ReturnsCreated()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = "Walked 30 minutes"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HealthLogResponse>>();
        Assert.Equal(200, body!.Code); // ApiResponse.Code is still 200
        Assert.Equal("EXERCISE", body.Data!.Type);
        Assert.Equal("Walked 30 minutes", body.Data!.Content);
        Assert.NotEqual(Guid.Empty, body.Data!.HealthLogId);
    }

    [Fact]
    public async Task PostHealthLog_ValidDiet_ReturnsCreated()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "Ate vegetables"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HealthLogResponse>>();
        Assert.Equal("DIET", body!.Data!.Type);
    }

    [Fact]
    public async Task PostHealthLog_ResponseContainsAllFields()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = "Running"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HealthLogResponse>>();
        Assert.NotNull(body!.Data!.HealthLogId);
        Assert.Equal(profile.PatientProfileId, body.Data.PatientProfileId);
        Assert.Equal("EXERCISE", body.Data.Type);
        Assert.Equal("Running", body.Data.Content);
        Assert.NotEqual(default(DateOnly), body.Data.LogDate);
        Assert.NotEqual(default(DateTime), body.Data.CreatedAt);
    }

    [Fact]
    public async Task PostHealthLog_MissingType_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        // Send request with null Type (will fail validation)
        var request = new LogHealthDataRequest { Type = null, Content = "Test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_EmptyContent_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = ""
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_WhitespaceContent_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "   "
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_InvalidType_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        var request = new LogHealthDataRequest
        {
            Type = "SLEEP",
            Content = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_NullBody_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        // Act
        var response = await client.PostAsJsonAsync<LogHealthDataRequest>("/api/v1/health-logs", null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        var request = new LogHealthDataRequest(); // Both fields null

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_NoAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // No auth header

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_DoctorAuth_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_NurseAuth_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateNurseClient(app);

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_AdminAuth_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateAdminClient(app);

        var request = new LogHealthDataRequest
        {
            Type = "Exercise",
            Content = "Test"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/health-logs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostHealthLog_MultipleOnSameDay_Accumulates()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var createdLogs = new List<HealthLog>();

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .Callback<HealthLog, CancellationToken>((log, _) => createdLogs.Add(log))
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        // Act - Create 3 logs on the same day
        for (int i = 0; i < 3; i++)
        {
            var request = new LogHealthDataRequest
            {
                Type = i % 2 == 0 ? "Exercise" : "DIET",
                Content = $"Log entry {i + 1}"
            };
            await client.PostAsJsonAsync("/api/v1/health-logs", request);
        }

        // Assert - All 3 should be created (accumulate behavior)
        Assert.Equal(3, createdLogs.Count);
    }

    #endregion

    #region GET /api/v1/health-logs Tests

    [Fact]
    public async Task GetHealthLogs_NoDate_ReturnsTodayLogs()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var logs = new List<HealthLog>
        {
            new() { HealthLogId = Guid.NewGuid(), PatientProfileId = profile.PatientProfileId, LogDate = today, LogType = HealthLogType.Exercise, Content = "Morning run", CreatedAt = DateTime.UtcNow },
            new() { HealthLogId = Guid.NewGuid(), PatientProfileId = profile.PatientProfileId, LogDate = today, LogType = HealthLogType.Diet, Content = "Lunch salad", CreatedAt = DateTime.UtcNow },
        };

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.GetByPatientAndDateAsync(profile.PatientProfileId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<HealthLogResponse>>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal(2, body.Data!.Count);
    }

    [Fact]
    public async Task GetHealthLogs_ValidDate_ReturnsLogs()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var specificDate = new DateOnly(2026, 8, 1);
        var logs = new List<HealthLog>
        {
            new() { HealthLogId = Guid.NewGuid(), PatientProfileId = profile.PatientProfileId, LogDate = specificDate, LogType = HealthLogType.Exercise, Content = "Old log", CreatedAt = DateTime.UtcNow },
        };

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.GetByPatientAndDateAsync(profile.PatientProfileId, specificDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var response = await client.GetAsync($"/api/v1/health-logs?date={specificDate:yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<HealthLogResponse>>>();
        Assert.Single(body!.Data!);
        Assert.Equal("Old log", body.Data![0].Content);
    }

    [Fact]
    public async Task GetHealthLogs_EmptyLogs_ReturnsEmptyArray()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.GetByPatientAndDateAsync(profile.PatientProfileId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>());

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<HealthLogResponse>>>();
        Assert.Empty(body!.Data!);
    }

    [Fact]
    public async Task GetHealthLogs_ResponseIsArray()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.GetByPatientAndDateAsync(profile.PatientProfileId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>());

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<HealthLogResponse>>>();
        Assert.NotNull(body!.Data);
        Assert.IsType<List<HealthLogResponse>>(body.Data);
    }

    [Fact]
    public async Task GetHealthLogs_ResponseFields_Present()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var logId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var logs = new List<HealthLog>
        {
            new() { HealthLogId = logId, PatientProfileId = profile.PatientProfileId, LogDate = today, LogType = HealthLogType.Exercise, Content = "Test", CreatedAt = createdAt },
        };

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.GetByPatientAndDateAsync(profile.PatientProfileId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<HealthLogResponse>>>();
        var log = body!.Data![0];
        Assert.Equal(logId, log.HealthLogId);
        Assert.Equal(profile.PatientProfileId, log.PatientProfileId);
        Assert.Equal(today, log.LogDate);
        Assert.Equal("EXERCISE", log.Type);
        Assert.Equal("Test", log.Content);
        Assert.Equal(createdAt, log.CreatedAt);
    }

    [Fact]
    public async Task GetHealthLogs_MalformedDate_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        // Act
        var response = await client.GetAsync("/api/v1/health-logs?date=invalid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthLogs_DateFormatWrong_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);

        // Act
        var response = await client.GetAsync("/api/v1/health-logs?date=01-08-2026");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthLogs_FutureDate_ReturnsEmptyArray()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreatePatientClient(app);
        var profile = NewPatientProfile(_patientId);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        _profiles.Setup(r => r.GetByUserIdAsync(_patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _healthLogs.Setup(r => r.GetByPatientAndDateAsync(profile.PatientProfileId, futureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>());

        // Act
        var response = await client.GetAsync($"/api/v1/health-logs?date={futureDate:yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<HealthLogResponse>>>();
        Assert.Empty(body!.Data!);
    }

    [Fact]
    public async Task GetHealthLogs_NoAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // No auth

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthLogs_DoctorAuth_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateDoctorClient(app);

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthLogs_NurseAuth_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = CreateNurseClient(app);

        // Act
        var response = await client.GetAsync("/api/v1/health-logs");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion
}
