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

/// <summary>
/// UC-06 — #17/#18/#19. Repository bị tráo bằng Mock qua DI, KHÔNG chạm Postgres/Supabase
/// thật — đúng pattern AccountStatusAuthTests.cs của Module 1.
/// </summary>
public class PatientProfilesControllerIntegrationTests
{
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly User _doctor = new()
    {
        UserId = Guid.NewGuid(), FullName = "BS. Lê Minh Hoàng", Phone = "0913456789",
        PasswordHash = "x", Role = UserRole.Doctor, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _patient = new()
    {
        UserId = Guid.NewGuid(), FullName = "Nguyễn Thị Hoa", Phone = "0981111001",
        PasswordHash = "x", Role = UserRole.Patient, Status = UserStatus.Active,
        DateOfBirth = new DateOnly(1992, 5, 14),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task PostPatientProfiles_ValidRequest_Returns201Created()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        _users.Setup(r => r.GetByIdAsync(_patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_patient);

        _users.Setup(r => r.GetByIdReadOnlyAsync(_patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_patient);

        _users.Setup(r => r.GetByIdReadOnlyAsync(_patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_patient);
        _profiles.Setup(r => r.ExistsForUserAsync(_patient.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        PatientProfile? saved = null;
        _profiles.Setup(r => r.AddAsync(It.IsAny<PatientProfile>(), It.IsAny<CancellationToken>()))
                 .Callback<PatientProfile, CancellationToken>((p, _) => { p.User = _patient; saved = p; })
                 .ReturnsAsync((PatientProfile p, CancellationToken _) => p);
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(() => saved);

        var request = new CreatePatientProfileRequest(_patient.UserId, "FEMALE", "Không có", null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patient-profiles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PatientProfileResponse>>();
        // ApiResponse<T>.Ok(...) hard-codes Code = 200 bất kể IActionResult bọc ngoài trả HTTP
        // status gì — quy ước có sẵn của toàn repo (vd. FeedbacksController cũng vậy), không
        // phải lỗi riêng của Module 04. HTTP status thật (201) đã được assert ở dòng trên.
        Assert.Equal(200, body!.Code);
        Assert.Equal(_patient.UserId, body.Data!.PatientUserId);
    }

    [Fact]
    public async Task PostPatientProfiles_PatientAlreadyHasProfile_Returns409Conflict()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        _users.Setup(r => r.GetByIdAsync(_patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_patient);

        _users.Setup(r => r.GetByIdReadOnlyAsync(_patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_patient);

        _users.Setup(r => r.GetByIdReadOnlyAsync(_patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_patient);
        _profiles.Setup(r => r.ExistsForUserAsync(_patient.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var request = new CreatePatientProfileRequest(_patient.UserId, "FEMALE", null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patient-profiles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(409, body!.Code);
    }

    [Fact]
    public async Task PostPatientProfiles_TargetAccountIsNotPatientRole_Returns422UnprocessableEntity()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        _users.Setup(r => r.GetByIdAsync(_doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_doctor);

        _users.Setup(r => r.GetByIdReadOnlyAsync(_doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_doctor);

        _users.Setup(r => r.GetByIdReadOnlyAsync(_doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_doctor);

        var request = new CreatePatientProfileRequest(_doctor.UserId, "FEMALE", null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patient-profiles", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostPatientProfiles_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange — chỉ Doctor/Nurse được tạo hồ sơ.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patient);

        var request = new CreatePatientProfileRequest(_patient.UserId, "FEMALE", null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patient-profiles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostPatientProfiles_NoToken_Returns401Unauthorized()
    {
        // Arrange
        using var app = MakeApp();
        var client = app.CreateClient(); // không gắn Authorization header

        var request = new CreatePatientProfileRequest(_patient.UserId, "FEMALE", null, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/patient-profiles", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientProfileById_NotFound_Returns404NotFound()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        _profiles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((PatientProfile?)null);

        // Act
        var response = await client.GetAsync($"/api/v1/patient-profiles/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(404, body!.Code);
    }

    [Fact]
    public async Task GetPatientProfileById_Found_Returns200WithProfile()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(), UserId = _patient.UserId, User = _patient,
            Gender = GenderType.Female, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);

        // Act
        var response = await client.GetAsync($"/api/v1/patient-profiles/{profile.PatientProfileId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PatientProfileResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal(profile.PatientProfileId, body.Data!.PatientProfileId);
        Assert.Equal(_patient.UserId, body.Data.PatientUserId);
    }

    [Fact]
    public async Task PutPatientProfile_ValidRequest_Returns200OkWithUpdatedFields()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(), UserId = _patient.UserId, User = _patient,
            Gender = GenderType.Female, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _profiles.Setup(r => r.GetForUpdateAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);

        var request = new UpdatePatientProfileRequest("MALE", "Cập nhật", null);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/patient-profiles/{profile.PatientProfileId}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PatientProfileResponse>>();
        Assert.Equal("MALE", body!.Data!.Gender);
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
        // chưa bị khoá (AccountStatusJwtEvents) — PHẢI mock caller ở đây, không chỉ mock các
        // UserId liên quan nghiệp vụ trong từng test, nếu không mọi request có token đều 401.
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
}
