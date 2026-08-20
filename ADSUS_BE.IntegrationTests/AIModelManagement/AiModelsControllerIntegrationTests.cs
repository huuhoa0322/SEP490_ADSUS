using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.AIModelManagement.Interfaces;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.AIModelManagement;

public class AiModelsControllerIntegrationTests
{
    private readonly Mock<IAiModelService> _aiModelService = new();
    private readonly Mock<IAiMetricsService> _aiMetricsService = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly User _admin = new()
    {
        UserId = Guid.NewGuid(), FullName = "Admin", Phone = "0999999999",
        PasswordHash = "x", Role = UserRole.Admin, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _doctor = new()
    {
        UserId = Guid.NewGuid(), FullName = "Doctor", Phone = "0933333333",
        PasswordHash = "x", Role = UserRole.Doctor, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _nurse = new()
    {
        UserId = Guid.NewGuid(), FullName = "Nurse", Phone = "0911111111",
        PasswordHash = "x", Role = UserRole.Nurse, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _patient = new()
    {
        UserId = Guid.NewGuid(), FullName = "Patient", Phone = "0922222222",
        PasswordHash = "x", Role = UserRole.Patient, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private WebApplicationFactory<Program> MakeApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiModelService>();
                services.AddScoped(_ => _aiModelService.Object);
                
                services.RemoveAll<IAiMetricsService>();
                services.AddScoped(_ => _aiMetricsService.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });

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

    // IT_Auth_01
    [Fact]
    public async Task SearchVersions_NoToken_Returns401Unauthorized()
    {
        using var app = MakeApp();
        var client = app.CreateClient(); // No token
        var response = await client.GetAsync("/api/v1/ai-model-versions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // IT_Auth_02
    [Theory]
    [InlineData("NURSE")]
    [InlineData("PATIENT")]
    [InlineData("DOCTOR")]
    public async Task SearchVersions_WrongRole_Returns403Forbidden(string role)
    {
        using var app = MakeApp();
        var user = role switch { "NURSE" => _nurse, "PATIENT" => _patient, _ => _doctor };
        var client = MakeClientWithToken(app, user);
        var response = await client.GetAsync("/api/v1/ai-model-versions");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // IT_Auth_02b — the full list stays Admin-only; a Doctor is not exempted just because
    // they can see the Active version through the dedicated endpoint below.
    [Fact]
    public async Task GetVersionById_DoctorRole_Returns403Forbidden()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var response = await client.GetAsync($"/api/v1/ai-model-versions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // IT_Auth_02c
    [Theory]
    [InlineData("NURSE")]
    [InlineData("PATIENT")]
    public async Task GetActiveVersion_WrongRole_Returns403Forbidden(string role)
    {
        using var app = MakeApp();
        var user = role == "NURSE" ? _nurse : _patient;
        var client = MakeClientWithToken(app, user);
        var response = await client.GetAsync("/api/v1/ai-model-versions/active");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // IT_Flow_01b — Doctor may see the Active version only (BR-07), via its own endpoint.
    [Fact]
    public async Task GetActiveVersion_DoctorRole_Returns200OK()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        _aiModelService.Setup(s => s.GetActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersionDto { VersionCode = "v1.0.0", Status = "Active" });

        var response = await client.GetAsync("/api/v1/ai-model-versions/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiModelVersionDto>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal("v1.0.0", body.Data!.VersionCode);
    }

    // IT_Flow_01
    [Fact]
    public async Task SearchVersions_ValidRequest_Returns200OK()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);

        _aiModelService.Setup(s => s.SearchVersionsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AiModelVersionDto>(new List<AiModelVersionDto>(), 1, 20, 0, 0));

        var response = await client.GetAsync("/api/v1/ai-model-versions");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AiModelVersionDto>>>();
        Assert.Equal(200, body!.Code);
    }

    // IT_Flow_02
    [Fact]
    public async Task GetVersionById_ValidId_Returns200OK()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        var id = Guid.NewGuid();

        _aiModelService.Setup(s => s.GetVersionByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersionDto());

        var response = await client.GetAsync($"/api/v1/ai-model-versions/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // IT_Val_01
    [Fact]
    public async Task RegisterVersion_MissingRequiredFields_Returns400BadRequest()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        
        // Missing VersionCode, HfRepoId, HfFilename
        var req = new { Description = "Test" };
        var response = await client.PostAsJsonAsync("/api/v1/ai-model-versions", req);
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // IT_Flow_03
    [Fact]
    public async Task RegisterVersion_ValidRequest_Returns201Created()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        
        var req = new RegisterModelVersionRequest { VersionCode = "v1", HfRepoId = "repo", HfFilename = "file" };
        var id = Guid.NewGuid();
        
        _aiModelService.Setup(s => s.RegisterVersionAsync(It.IsAny<RegisterModelVersionRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersionDto { ModelVersionId = id });

        var response = await client.PostAsJsonAsync("/api/v1/ai-model-versions", req);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location.ToString());
    }

    // IT_Flow_04
    [Fact]
    public async Task UpdateVersion_ValidRequest_Returns200OK()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        
        var req = new UpdateModelVersionRequest { Description = "New Desc", HfRepoId = "repo", HfFilename = "file" };
        var id = Guid.NewGuid();
        
        _aiModelService.Setup(s => s.UpdateVersionAsync(id, It.IsAny<UpdateModelVersionRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PutAsJsonAsync($"/api/v1/ai-model-versions/{id}", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // IT_Val_02
    [Fact]
    public async Task ActivateVersion_InvalidStatus_Returns422UnprocessableEntity()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        
        var req = new ActivateVersionRequest { Status = "INACTIVE" }; // Not ACTIVE
        var id = Guid.NewGuid();

        var response = await client.PatchAsJsonAsync($"/api/v1/ai-model-versions/{id}", req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // IT_Flow_05
    [Fact]
    public async Task ActivateVersion_ValidRequest_Returns200OK()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        
        var req = new ActivateVersionRequest { Status = "ACTIVE" };
        var id = Guid.NewGuid();
        
        _aiModelService.Setup(s => s.ActivateVersionAsync(id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PatchAsJsonAsync($"/api/v1/ai-model-versions/{id}", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // IT_Flow_05b
    [Fact]
    public async Task ActivateVersion_ServiceThrowsTaskCanceled_Returns500InternalServerError()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        
        var req = new ActivateVersionRequest { Status = "ACTIVE" };
        var id = Guid.NewGuid();
        
        _aiModelService.Setup(s => s.ActivateVersionAsync(id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Timeout from HttpClient"));

        var response = await client.PatchAsJsonAsync($"/api/v1/ai-model-versions/{id}", req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // IT_Flow_06
    [Fact]
    public async Task CalculateMap50_ValidRequest_Returns200OK()
    {
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _admin);
        var id = Guid.NewGuid();
        
        _aiMetricsService.Setup(s => s.CalculateMap50Async(id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PostAsync($"/api/v1/ai-model-versions/{id}/calculate-map50", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
