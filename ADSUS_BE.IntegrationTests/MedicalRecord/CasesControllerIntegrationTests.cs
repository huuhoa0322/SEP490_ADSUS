using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.MedicalRecord;

/// <summary>UC-07, UC-08, UC-12 — #20–#25, #27.</summary>
public class CasesControllerIntegrationTests
{
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<IUltrasoundImageRepository> _images = new();
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFileStorageService> _storage = new();

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

    private readonly User _nurse = new()
    {
        UserId = Guid.NewGuid(), FullName = "ĐD. Võ Thị Thu Hà", Phone = "0915678901",
        PasswordHash = "x", Role = UserRole.Nurse, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private PatientProfile MakePatientProfile() => new()
    {
        PatientProfileId = Guid.NewGuid(), UserId = _patientUser.UserId, User = _patientUser,
        Gender = GenderType.Female, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private Case MakeCase(PatientProfile profile, CaseStatus status) => new()
    {
        CaseId = Guid.NewGuid(), PatientProfileId = profile.PatientProfileId, PatientProfile = profile,
        DoctorId = _doctor.UserId, Doctor = _doctor,
        VisitDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = status,
        FinalDiagnosis = status == CaseStatus.Confirmed ? "U tuyến xơ vú phải (BI-RADS 3)" : null,
        DoctorConclusion = status == CaseStatus.Confirmed ? "Theo dõi định kỳ" : null,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    // ---------- #23 GET /cases/{id} ----------

    [Fact]
    public async Task GetCaseById_CalledByDoctor_Returns200WithFullStaffShape()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{medicalCase.CaseId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.NotNull(body.Data!.PatientProfile); // chỉ bản Staff mới có field này
    }

    [Fact]
    public async Task GetCaseById_CalledByOwningPatientOnConfirmedCase_Returns200WithPatientShape()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);
        _profiles.Setup(r => r.GetByUserIdAsync(_patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{medicalCase.CaseId}");

        // Assert — dùng JsonDocument thay vì ApiResponse<CaseResponse> vì shape thật trả về
        // là PatientCaseResponse (ít field hơn) khi caller là Patient.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        var data = json!.RootElement.GetProperty("data");
        Assert.False(data.TryGetProperty("clinicalInfo", out _));
        Assert.False(data.TryGetProperty("ultrasoundImages", out _));
    }

    [Fact]
    public async Task GetCaseById_PatientRequestsOwnUnconfirmedCase_Returns404NotFound()
    {
        // Arrange — GB-05: ca chưa duyệt của chính họ vẫn phải bị giấu.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);
        var profile = MakePatientProfile();
        var pendingCase = MakeCase(profile, CaseStatus.Created);
        _profiles.Setup(r => r.GetByUserIdAsync(_patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.GetDetailAsync(pendingCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(pendingCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{pendingCase.CaseId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCaseById_PatientRequestsAnotherPatientsConfirmedCase_Returns404NotFound()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);
        var callerProfile = MakePatientProfile();
        var someoneElsesCase = MakeCase(MakePatientProfile(), CaseStatus.Confirmed);
        _profiles.Setup(r => r.GetByUserIdAsync(_patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(callerProfile);
        _cases.Setup(r => r.GetDetailAsync(someoneElsesCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(someoneElsesCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{someoneElsesCase.CaseId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- #25 GET /cases/me ----------

    [Fact]
    public async Task GetCasesMe_PatientCaller_Returns200AndDoesNotAcceptStatusParam()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);
        var profile = MakePatientProfile();
        _profiles.Setup(r => r.GetByUserIdAsync(_patientUser.UserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.SearchByPatientAsync(
                  profile.PatientProfileId, CaseStatus.Confirmed, "desc", 1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((new List<Case>(), 0));

        // Act
        var response = await client.GetAsync("/api/v1/cases/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _cases.Verify(r => r.SearchByPatientAsync(
            profile.PatientProfileId, CaseStatus.Confirmed, "desc", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCasesMe_CalledByDoctor_Returns403Forbidden()
    {
        // Arrange — route dành riêng cho Patient; đồng thời chứng minh {id:guid} không nuốt
        // nhầm "me" thành 400 (nếu bị nuốt nhầm sẽ ra 400, không phải 403).
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        // Act
        var response = await client.GetAsync("/api/v1/cases/me");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- #24 GET /cases?patientProfileId= ----------

    [Fact]
    public async Task GetCasesByPatientProfileId_MissingParam_Returns400BadRequest()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);

        // Act
        var response = await client.GetAsync("/api/v1/cases");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCasesByPatientProfileId_ValidRequest_Returns200OkWithPagedResult()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.SearchByPatientAsync(
                  profile.PatientProfileId, null, "desc", 1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((new List<Case>(), 0));

        // Act
        var response = await client.GetAsync($"/api/v1/cases?patientProfileId={profile.PatientProfileId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCasesByPatientProfileId_ReturnsCreatedAt()
    {
        // Thêm 06/08/2026 — #24 (StaffCaseSummaryResponse) mang thêm CreatedAt so với #25,
        // kiểm qua cả pipeline HTTP thật để bắt lỗi serialize (không chỉ ở tầng service).
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Created);
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _cases.Setup(r => r.SearchByPatientAsync(
                  profile.PatientProfileId, null, "desc", 1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((new List<Case> { medicalCase }, 1));

        // Act
        var response = await client.GetAsync($"/api/v1/cases?patientProfileId={profile.PatientProfileId}");

        // Assert
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponse<PagedResult<StaffCaseSummaryResponse>>>();
        Assert.Equal(medicalCase.CreatedAt, body!.Data!.Items.Single().CreatedAt);
    }

    [Fact]
    public async Task GetCasesByPatientProfileId_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);

        // Act
        var response = await client.GetAsync($"/api/v1/cases?patientProfileId={Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- #22 GET /cases/{caseId}/ultrasound-images ----------

    [Fact]
    public async Task GetUltrasoundImages_CaseExists_Returns200WithImageList()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Created);
        var image = new UltrasoundImage
        {
            ImageId = Guid.NewGuid(), CaseId = medicalCase.CaseId,
            FileRef = "path/anh.png", UploadedAt = DateTime.UtcNow,
        };
        _cases.Setup(r => r.GetByIdAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _images.Setup(r => r.ListByCaseAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<UltrasoundImage> { image });
        _storage.Setup(s => s.CreateSignedUrlAsync(image.FileRef, It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://signed-url.example/anh.png");

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{medicalCase.CaseId}/ultrasound-images");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponse<IReadOnlyList<UltrasoundImageResponse>>>();
        Assert.Single(body!.Data!);
    }

    [Fact]
    public async Task GetUltrasoundImages_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange — chỉ Doctor/Nurse, kể cả với ca của chính họ.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{Guid.NewGuid()}/ultrasound-images");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- helpers (dùng chung cho cả Task 13, 14 nối tiếp vào file này) ----

    private static MultipartFormDataContent MakeCreateCaseForm(
        Guid patientProfileId, Guid responsibleDoctorId, string? clinicalInfo, byte[]? imageBytes)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(patientProfileId.ToString()), "patientProfileId" },
            { new StringContent(responsibleDoctorId.ToString()), "responsibleDoctorId" },
        };

        if (clinicalInfo is not null)
        {
            form.Add(new StringContent(clinicalInfo), "clinicalInfo");
        }

        if (imageBytes is not null)
        {
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(imageContent, "images", "anh.png");
        }

        return form;
    }

    private static readonly byte[] ValidPngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

    private WebApplicationFactory<Program> MakeApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICaseRepository>();
                services.AddScoped(_ => _cases.Object);
                services.RemoveAll<IUltrasoundImageRepository>();
                services.AddScoped(_ => _images.Object);
                services.RemoveAll<IPatientProfileRepository>();
                services.AddScoped(_ => _profiles.Object);
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
                services.RemoveAll<IFileStorageService>();
                services.AddScoped(_ => _storage.Object);
            });
        });

    private HttpClient MakeClientWithToken(WebApplicationFactory<Program> app, User caller)
    {
        // Pipeline xác thực gọi IUserRepository.GetByIdAsync(callerId) để kiểm tra tài khoản
        // chưa bị khoá (AccountStatusJwtEvents) — PHẢI mock caller ở đây (bài học từ Task 10).
        _users.Setup(r => r.GetByIdReadOnlyAsync(caller.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(caller);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(caller);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------- #20 POST /cases ----------

    [Fact]
    public async Task PostCases_ValidMultipartRequest_Returns201Created()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();

        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(_doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_doctor);
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) => path);

        Case? createdCase = null;
        _cases.Setup(r => r.CreateWithImagesAsync(
                  It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
              .Callback<Case, IReadOnlyList<UltrasoundImage>, CancellationToken>((c, imgs, _) =>
              {
                  c.PatientProfile = profile;
                  c.Doctor = _doctor;
                  c.UltrasoundImages = imgs.ToList();
                  createdCase = c;
              })
              .ReturnsAsync((Case c, IReadOnlyList<UltrasoundImage> _, CancellationToken _) => c);
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => createdCase);

        using var form = MakeCreateCaseForm(profile.PatientProfileId, _doctor.UserId, "Đau vú trái", ValidPngBytes);

        // Act
        var response = await client.PostAsync("/api/v1/cases", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>();
        Assert.Equal("CREATED", body!.Data!.Status);
    }

    [Fact]
    public async Task PostCases_NoImageAttached_Returns201Created()
    {
        // Arrange — quyết định ghi đè 07/08/2026: #20 không còn bắt buộc ảnh nữa (#21 —
        // AddUltrasoundImagesAsync — vẫn bắt buộc, xem PostUltrasoundImages_NoImageAttached_Returns400BadRequest).
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(_doctor.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(_doctor);

        Case? createdCase = null;
        _cases.Setup(r => r.CreateWithImagesAsync(
                  It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
              .Callback<Case, IReadOnlyList<UltrasoundImage>, CancellationToken>((c, imgs, _) =>
              {
                  createdCase = c;
                  c.UltrasoundImages = imgs.ToList();
                  c.PatientProfile = profile;
                  c.Doctor = _doctor;
              })
              .ReturnsAsync((Case c, IReadOnlyList<UltrasoundImage> _, CancellationToken _) => c);
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => createdCase);

        using var form = MakeCreateCaseForm(profile.PatientProfileId, _doctor.UserId, null, imageBytes: null);

        // Act
        var response = await client.PostAsync("/api/v1/cases", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>();
        Assert.Empty(body!.Data!.UltrasoundImages);
    }

    [Fact]
    public async Task PostCases_ResponsibleDoctorIsNurseAccount_Returns422UnprocessableEntity()
    {
        // Arrange — GB-04.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var nurse = new User
        {
            UserId = Guid.NewGuid(), FullName = "ĐD. Võ Thị Thu Hà", Phone = "0915678901",
            PasswordHash = "x", Role = UserRole.Nurse, Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _profiles.Setup(r => r.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(profile);
        _users.Setup(r => r.GetByIdAsync(nurse.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(nurse);

        using var form = MakeCreateCaseForm(profile.PatientProfileId, nurse.UserId, null, ValidPngBytes);

        // Act
        var response = await client.PostAsync("/api/v1/cases", form);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostCases_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);
        using var form = MakeCreateCaseForm(Guid.NewGuid(), Guid.NewGuid(), null, ValidPngBytes);

        // Act
        var response = await client.PostAsync("/api/v1/cases", form);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- #21 POST /cases/{caseId}/ultrasound-images ----------

    [Fact]
    public async Task PostUltrasoundImages_ValidRequestOnCreatedCase_Returns201Created()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Created);
        _cases.Setup(r => r.GetByIdAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _storage.Setup(s => s.UploadAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream _, string path, string _, CancellationToken _) => path);

        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(ValidPngBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "images", "anh2.png");
        form.Add(new StringContent("Ảnh góc nghiêng"), "note");

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{medicalCase.CaseId}/ultrasound-images", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostUltrasoundImages_NoImageAttached_Returns400BadRequest()
    {
        // Arrange — NGƯỢC #20: #21 trả 400 cho cùng lỗi (flag N2).
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        using var form = new MultipartFormDataContent();

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{Guid.NewGuid()}/ultrasound-images", form);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostUltrasoundImages_CaseAlreadyConfirmed_Returns422UnprocessableEntity()
    {
        // Arrange — GB-01.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var confirmedCase = MakeCase(profile, CaseStatus.Confirmed);
        _cases.Setup(r => r.GetByIdAsync(confirmedCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(confirmedCase);

        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(ValidPngBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "images", "anh.png");

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{confirmedCase.CaseId}/ultrasound-images", form);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostUltrasoundImages_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);
        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(ValidPngBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "images", "anh.png");

        // Act
        var response = await client.PostAsync($"/api/v1/cases/{Guid.NewGuid()}/ultrasound-images", form);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- #27 GET /cases/{id}/report ----------

    [Fact]
    public async Task GetCaseReport_ConfirmedCase_Returns422WithJsonEnvelope()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{medicalCase.CaseId}/report");

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(422, body!.Code);
    }

    [Fact]
    public async Task GetCaseReport_EndCase_Returns200WithPdfContentType()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.End);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{medicalCase.CaseId}/report");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task GetCaseReport_CreatedCase_Returns422WithJsonEnvelope()
    {
        // Arrange — AF-01: nhánh lỗi vẫn dùng khuôn JSON như mọi endpoint khác.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var pendingCase = MakeCase(profile, CaseStatus.Created);
        _cases.Setup(r => r.GetDetailAsync(pendingCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(pendingCase);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{pendingCase.CaseId}/report");

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(422, body!.Code);
    }

    [Fact]
    public async Task GetCaseReport_CalledByPatientRole_Returns403Forbidden()
    {
        // Arrange — quyết định UCS 01/08/2026: Patient bị loại trừ tường minh, kể cả ca của
        // chính họ đã CONFIRMED — chỉ Doctor/Nurse mới xuất được PDF.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _patientUser);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{Guid.NewGuid()}/report");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCaseReport_CaseNotFound_Returns404WithJsonEnvelope()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Case?)null);

        // Act
        var response = await client.GetAsync($"/api/v1/cases/{Guid.NewGuid()}/report");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal(404, body!.Code);
    }

    // ---------- PUT /cases/{id}/conclusion và /confirm (sửa lại 07/08/2026) ----------

    private static CaseConclusionRequest ValidConfirmBody() => new(
        FinalDiagnosis: "Nhân xơ tử cung", DoctorConclusion: "Theo dõi định kỳ sau 6 tháng");

    [Fact]
    public async Task PutConfirm_ValidRequestByResponsibleDoctor_Returns200WithConfirmedStatus()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.End);
        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{medicalCase.CaseId}/confirm", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>();
        Assert.Equal("CONFIRMED", body!.Data!.Status);
        Assert.Equal("Nhân xơ tử cung", body.Data.FinalDiagnosis);
        _cases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutConfirm_CalledByNurse_Returns403Forbidden()
    {
        // Arrange — CHỈ Bác sĩ mới chốt được kết luận, Điều dưỡng không có quyền này.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _nurse);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/cases/{Guid.NewGuid()}/confirm", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _cases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PutConfirm_ActingDoctorIsNotResponsibleDoctor_Returns422UnprocessableEntity()
    {
        // Arrange — GB-04: bác sĩ khác không phải người phụ trách ca này.
        var otherDoctor = new User
        {
            UserId = Guid.NewGuid(), FullName = "BS. Nguyễn Văn An", Phone = "0913456700",
            PasswordHash = "x", Role = UserRole.Doctor, Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        using var app = MakeApp();
        var client = MakeClientWithToken(app, otherDoctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.End);
        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{medicalCase.CaseId}/confirm", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PutConfirm_CaseAlreadyConfirmed_Returns422UnprocessableEntity()
    {
        // Arrange — P2/GB-01: trạng thái cuối, không có đường lùi, kể cả chốt lại đúng nội dung cũ.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);
        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{medicalCase.CaseId}/confirm", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PutConfirm_EmptyConclusion_Returns400BadRequest()
    {
        // Arrange — validator chặn trước khi chạm tới service.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var body = new CaseConclusionRequest(FinalDiagnosis: "", DoctorConclusion: "");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{Guid.NewGuid()}/confirm", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _cases.Verify(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PutConclusion_ValidRequestByResponsibleDoctor_Returns200WithoutChangingStatus()
    {
        // Arrange — "Lưu kết luận" (sửa lại 07/08/2026, tách khỏi /confirm): lưu nội dung,
        // KHÔNG đổi trạng thái ca, khác hẳn /confirm ("Kết thúc ca khám").
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.End);
        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{medicalCase.CaseId}/conclusion", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>();
        Assert.Equal("END", body!.Data!.Status);
        Assert.Equal("Nhân xơ tử cung", body.Data.FinalDiagnosis);
        _cases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutConclusion_CalledByNurse_Returns403Forbidden()
    {
        // Arrange — CHỈ Bác sĩ, cùng luật với /confirm.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _nurse);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/cases/{Guid.NewGuid()}/conclusion", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _cases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PutConclusion_CaseAlreadyConfirmed_Returns422UnprocessableEntity()
    {
        // Arrange — P2/GB-01: ca đã khoá thì "Lưu kết luận" cũng bị từ chối, không chỉ /confirm.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);
        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{medicalCase.CaseId}/conclusion", ValidConfirmBody());

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PutConclusion_EmptyConclusion_Returns400BadRequest()
    {
        // Arrange — validator chặn trước khi chạm tới service.
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var body = new CaseConclusionRequest(FinalDiagnosis: "", DoctorConclusion: "");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{Guid.NewGuid()}/conclusion", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _cases.Verify(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PutConclusion_Over5000Chars_Returns400BadRequest()
    {
        // Arrange — validator chặn (IT_Val_05)
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var longText = new string('A', 5001);
        var body = new CaseConclusionRequest(FinalDiagnosis: longText, DoctorConclusion: "OK");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{Guid.NewGuid()}/conclusion", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutConclusion_MissingNameIdentifierClaim_ThrowsUnauthorizedAccessExceptionAndReturns401()
    {
        // Arrange — IT_Auth_05: Token hợp lệ nhưng thiếu Claim NameIdentifier
        using var app = MakeApp();
        var mockAuthService = new Mock<IAuthService>();
        var client = app.CreateClient(); 
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "NoNameIdentifier");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cases/{Guid.NewGuid()}/conclusion", ValidConfirmBody());

        // Assert
        Assert.False(response.IsSuccessStatusCode);
    }

    // ---------- #xx PUT /cases/{id}/end ----------

    [Fact]
    public async Task PutEnd_CalledByResponsibleDoctor_Returns200AndChangesStatus()
    {
        // Arrange
        using var app = MakeApp();
        var client = MakeClientWithToken(app, _doctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);

        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsync($"/api/v1/cases/{medicalCase.CaseId}/end", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>();
        Assert.Equal(200, body!.Code);
        Assert.Equal("Case ended successfully without prescription", body.Message);
        
        _cases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(CaseStatus.End, medicalCase.Status);
    }

    [Fact]
    public async Task PutEnd_CalledByDifferentDoctor_Returns422()
    {
        // Arrange
        using var app = MakeApp();
        var otherDoctor = new User { UserId = Guid.NewGuid(), FullName = "BS Khác", Phone = "0123", Role = UserRole.Doctor, Status = UserStatus.Active };
        var client = MakeClientWithToken(app, otherDoctor);
        var profile = MakePatientProfile();
        var medicalCase = MakeCase(profile, CaseStatus.Confirmed);

        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var response = await client.PutAsync($"/api/v1/cases/{medicalCase.CaseId}/end", null);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("Only the responsible doctor can end this case.", body!.Message);
        _cases.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
