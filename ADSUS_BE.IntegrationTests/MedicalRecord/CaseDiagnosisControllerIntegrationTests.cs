using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

public class CaseDiagnosisControllerIntegrationTests
{
    private readonly Mock<ICaseDiagnosisService> _diagnosisService = new();
    private readonly Mock<IUserRepository> _users = new();

    private readonly User _doctor = new()
    {
        UserId = Guid.NewGuid(), FullName = "BS. Lê Minh", Phone = "0900000000",
        PasswordHash = "x", Role = UserRole.Doctor, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _nurse = new()
    {
        UserId = Guid.NewGuid(), FullName = "ĐD. Hà", Phone = "0911111111",
        PasswordHash = "x", Role = UserRole.Nurse, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private readonly User _patient = new()
    {
        UserId = Guid.NewGuid(), FullName = "BN. Hoa", Phone = "0922222222",
        PasswordHash = "x", Role = UserRole.Patient, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private WebApplicationFactory<Program> MakeApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseDiagnosisService>();
                services.AddScoped(_ => _diagnosisService.Object);
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

    private MultipartFormDataContent MakeAnalyzePayload(bool withImage)
    {
        var form = new MultipartFormDataContent();

        
        if (withImage)
        {
            var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            form.Add(content, "Image", "test.png");
        }
        return form;
    }

    private MultipartFormDataContent MakeConfirmPayload(bool missingOrig, bool missingBurnt)
    {
        var form = new MultipartFormDataContent();

        form.Add(new StringContent("[]"), "AiPredictionsJson");
        form.Add(new StringContent("[]"), "DoctorAnnotationsJson");
        
        if (!missingOrig)
        {
            var content1 = new ByteArrayContent(new byte[] { 1, 2, 3 });
            content1.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            form.Add(content1, "OriginalImage", "orig.png");
        }
        if (!missingBurnt)
        {
            var content2 = new ByteArrayContent(new byte[] { 1, 2, 3 });
            content2.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            form.Add(content2, "BurntImage", "burnt.png");
        }

        return form;
    }

    // =========================================================================
    // Auth & Access Control Tests (IT_Auth_01 -> IT_Auth_04)
    // =========================================================================

    [Fact]
    public async Task AnalyzeImage_NoToken_Returns401Unauthorized()
    {
        using var app = MakeApp();
        var client = app.CreateClient(); // No token
        var response = await client.PostAsync($"/api/v1/cases/{Guid.NewGuid()}/analyze", MakeAnalyzePayload(true));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("NURSE")]
    [InlineData("PATIENT")]
    public async Task AnalyzeImage_WrongRole_Returns403Forbidden(string role)
    {
        using var app = MakeApp();
        var user = role == "NURSE" ? _nurse : _patient;
        var client = MakeClientWithToken(app, user);
        var response = await client.PostAsync($"/api/v1/cases/{Guid.NewGuid()}/analyze", MakeAnalyzePayload(true));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // Validation Tests (IT_Val_01, IT_Val_02)
    // =========================================================================

    [Fact]
    public async Task AnalyzeImage_MissingImage_Returns400BadRequest()
    {
        // IT_Val_01
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var response = await client.PostAsync($"/api/v1/cases/{Guid.NewGuid()}/analyze", MakeAnalyzePayload(false));
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ConfirmAnalysis_MissingImages_Returns400BadRequest(bool missOrig, bool missBurnt)
    {
        // IT_Val_02
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var response = await client.PostAsync($"/api/v1/cases/{Guid.NewGuid()}/images/confirm", MakeConfirmPayload(missOrig, missBurnt));
        
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Integration Flows (IT_Flow_01, IT_Flow_02)
    // =========================================================================

    [Fact]
    public async Task AnalyzeImage_ValidRequest_CallsServiceAndReturns200()
    {
        // IT_Flow_01
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var caseId = Guid.NewGuid();

        var mockJson = JsonDocument.Parse("[]").RootElement;
        _diagnosisService.Setup(s => s.AnalyzeImageAsync(caseId, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJson);

        var response = await client.PostAsync($"/api/v1/cases/{caseId}/analyze", MakeAnalyzePayload(true));
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(200, body!.Code);
        
        _diagnosisService.Verify(s => s.AnalyzeImageAsync(caseId, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeImage_ServiceThrowsException_Returns500InternalServerError()
    {
        // IT_Flow_01b: Verify GlobalExceptionHandler catches TaskCanceledException (Timeout) or InvalidOperationException from Service
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var caseId = Guid.NewGuid();

        _diagnosisService.Setup(s => s.AnalyzeImageAsync(caseId, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Timeout from HttpClient"));

        var response = await client.PostAsync($"/api/v1/cases/{caseId}/analyze", MakeAnalyzePayload(true));
        
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(500, body!.Code);
        Assert.Equal("An unexpected error occurred. Please try again later.", body.Message);
    }

    [Fact]
    public async Task ConfirmAnalysis_ValidRequest_CallsServiceAndReturns200()
    {
        // IT_Flow_02
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var caseId = Guid.NewGuid();

        _diagnosisService.Setup(s => s.ConfirmAnalysisAsync(caseId, It.IsAny<ConfirmAnalysisRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PostAsync($"/api/v1/cases/{caseId}/images/confirm", MakeConfirmPayload(false, false));
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal("Image and annotations saved successfully", body.Message);

        _diagnosisService.Verify(s => s.ConfirmAnalysisAsync(caseId, It.IsAny<ConfirmAnalysisRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
