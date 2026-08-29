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

public class SymptomsControllerIntegrationTests
{
    private readonly Mock<ISymptomService> _symptoms = new();
    private readonly Mock<IUserRepository> _users = new();

    // [Authorize] trần ở SymptomsController — không giới hạn theo role, mọi tài khoản đã đăng
    // nhập đều dùng được để render UI tạo ca khám.
    private readonly User _nurseCaller = MakeUser(UserRole.Nurse, "ĐD. Võ Thị Thu Hà");

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
                services.RemoveAll<ISymptomService>();
                services.AddScoped(_ => _symptoms.Object);
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
    public async Task GetCategories_AuthenticatedCaller_Returns200WithNestedSymptoms()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var symptomId = Guid.NewGuid();
        _symptoms.Setup(s => s.GetCategoriesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<SymptomCategoryResponse>
                 {
                     new(categoryId, "Đau vú", false,
                         new List<SymptomItemResponse> { new(symptomId, "Đau khi chạm", false) }),
                 });

        await using var app = CreateApp();
        var client = MakeClientWithToken(app, _nurseCaller);

        // Act
        var response = await client.GetAsync("/api/v1/symptoms/categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<SymptomCategoryResponse>>>();
        var category = body!.Data!.Single();
        Assert.Equal("Đau vú", category.Name);
        Assert.Equal("Đau khi chạm", category.Symptoms.Single().Name);
    }

    [Fact]
    public async Task GetCategories_NoToken_Returns401Unauthorized()
    {
        // Arrange
        await using var app = CreateApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/symptoms/categories");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
