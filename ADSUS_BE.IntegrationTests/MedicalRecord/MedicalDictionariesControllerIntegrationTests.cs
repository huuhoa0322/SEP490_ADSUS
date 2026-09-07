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

// Controller này vừa được tách khỏi AppDbContext trực tiếp trong đợt P11 review (29/08/2026,
// xem MedicalDictionariesController.cs) — integration test đầu tiên xác nhận layer mới
// (Repository -> Service -> Controller) trả đúng dữ liệu qua HTTP thật.
public class MedicalDictionariesControllerIntegrationTests
{
    private readonly Mock<IMedicalDictionaryService> _medicalDictionaries = new();
    private readonly Mock<IUserRepository> _users = new();

    // [Authorize] trần — không giới hạn theo role, mọi tài khoản đã đăng nhập đều dùng được.
    private readonly User _doctorCaller = MakeUser(UserRole.Doctor, "BS. Lê Minh Hoàng");

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
                services.RemoveAll<IMedicalDictionaryService>();
                services.AddScoped(_ => _medicalDictionaries.Object);
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            }));

    private HttpClient MakeClientWithToken(WebApplicationFactory<Program> app, User caller)
    {
        _users.Setup(r => r.GetByIdAsync(caller.UserId, It.IsAny<CancellationToken>()))
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
    public async Task GetDiseases_AuthenticatedCaller_Returns200WithList()
    {
        // Arrange
        var diseaseId = Guid.NewGuid();
        _medicalDictionaries.Setup(s => s.GetDiseasesAsync(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(new List<MedicalDiseaseResponse>
                             {
                                 new(diseaseId, "Tiểu đường", true, false),
                             });

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _doctorCaller);

        // Act
        var response = await client.GetAsync("/api/v1/medical-dictionaries/diseases");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<MedicalDiseaseResponse>>>();
        Assert.Equal("Tiểu đường", body!.Data!.Single().Name);
    }

    [Fact]
    public async Task GetDiseases_NoToken_Returns401Unauthorized()
    {
        // Arrange
        await using var app = CreateApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/medical-dictionaries/diseases");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllergyTypes_AuthenticatedCaller_Returns200WithList()
    {
        // Arrange
        var allergyTypeId = Guid.NewGuid();
        _medicalDictionaries.Setup(s => s.GetAllergyTypesAsync(It.IsAny<CancellationToken>()))
                             .ReturnsAsync(new List<MedicalAllergyTypeResponse>
                             {
                                 new(allergyTypeId, "Dị ứng thuốc kháng sinh", false),
                             });

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _doctorCaller);

        // Act
        var response = await client.GetAsync("/api/v1/medical-dictionaries/allergy-types");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<MedicalAllergyTypeResponse>>>();
        Assert.Equal("Dị ứng thuốc kháng sinh", body!.Data!.Single().Name);
    }

    [Fact]
    public async Task GetAllergyTypes_NoToken_Returns401Unauthorized()
    {
        // Arrange
        await using var app = CreateApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/medical-dictionaries/allergy-types");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
