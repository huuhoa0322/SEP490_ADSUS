using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF02_AiModelLifecycle;

/// <summary>
/// Report 5.3 — BF-02 AI Model Lifecycle (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật,
/// và ActivateVersion còn gọi THẬT sang AI Python Backend (AiBackend:WebhookUrl,
/// {url}/api/reload-model) để nạp model từ HuggingFace — không mock, đúng tinh thần System Test.
/// Backend Python phải đang chạy và Repo/Filename dưới đây phải tồn tại thật trên HuggingFace,
/// nếu không toàn bộ test có activate sẽ Fail đúng nghĩa (không phải lỗi test).
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01) trước khi chạy.
/// </summary>
public class AiModelLifecycleTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    // Model thật, nhẹ, đã xác nhận tồn tại trên HuggingFace — dùng chung cho mọi test cần
    // activate thành công. Nhiều AiModelVersion khác nhau (VersionCode khác nhau) được phép
    // cùng trỏ tới 1 repo/file này — không có ràng buộc unique nào trên HfRepoId/HfFilename.
    private const string RealHfRepoId = "tranqui247/adsus";
    private const string RealHfFilename = "YOLO26_EffNetV2S_BFV2_512.pt";

    [Fact]
    public async Task STC001_AdminRegistersModelVersion_Returns201Inactive()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var versionCode = UniqueVersionCode();
        var response = await admin.PostAsJsonAsync("/api/v1/ai-model-versions", new
        {
            versionCode,
            hfRepoId = RealHfRepoId,
            hfFilename = RealHfFilename,
            description = "STC001 registration test",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiModelVersionDto>>(ct);
        Assert.Equal(versionCode, body!.Data!.VersionCode);
        Assert.Equal("Inactive", body.Data.Status);
    }

    [Fact]
    public async Task STC002_RegisterModelVersion_DuplicateVersionCode_Returns422()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var versionCode = UniqueVersionCode();
        var firstResponse = await RegisterVersionAsync(admin, versionCode, ct);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await RegisterVersionAsync(admin, versionCode, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicateResponse.StatusCode);
        var body = await duplicateResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Contains("đã tồn tại", body!.Message);
    }

    [Fact]
    public async Task STC003_AdminUpdatesVersionInfo_WhileInactive_ChangesAreSaved()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var registerResponse = await RegisterVersionAsync(admin, UniqueVersionCode(), ct);
        var registered = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AiModelVersionDto>>(ct);
        var versionId = registered!.Data!.ModelVersionId;

        const string updatedDescription = "STC003 updated description";
        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/ai-model-versions/{versionId}", new
        {
            description = updatedDescription,
            hfRepoId = RealHfRepoId,
            hfFilename = RealHfFilename,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await admin.GetAsync($"/api/v1/ai-model-versions/{versionId}", ct);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<AiModelVersionDto>>(ct);
        Assert.Equal(updatedDescription, fetched!.Data!.Description);
    }

    [Fact]
    public async Task STC004_AdminUpdatesVersionInfo_WhileActive_Returns422()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var versionId = await RegisterAndActivateAsync(admin, UniqueVersionCode(), ct);

        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/ai-model-versions/{versionId}", new
        {
            description = "STC004 attempted update on an Active version",
            hfRepoId = RealHfRepoId,
            hfFilename = RealHfFilename,
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Contains("ACTIVE", body!.Message);
    }

    [Fact]
    public async Task STC005_ActivateVersion_EnforcesSingleActiveConstraint_AndSupportsRollback()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        // Kích hoạt version 1 — chưa có version Active nào trước đó.
        var versionOneId = await RegisterAndActivateAsync(admin, UniqueVersionCode(), ct);
        Assert.Equal("Active", await GetStatusAsync(admin, versionOneId, ct));

        // Kích hoạt version 2 — version 1 phải tự động chuyển Inactive (chỉ 1 Active tại 1 thời điểm).
        var versionTwoId = await RegisterAndActivateAsync(admin, UniqueVersionCode(), ct);
        Assert.Equal("Active", await GetStatusAsync(admin, versionTwoId, ct));
        Assert.Equal("Inactive", await GetStatusAsync(admin, versionOneId, ct));

        // Rollback — kích hoạt lại version 1 (cũ hơn); version 2 phải tự động chuyển Inactive.
        var rollbackResponse = await admin.PatchAsync($"/api/v1/ai-model-versions/{versionOneId}", ToJsonContent(new { status = "ACTIVE" }), ct);
        Assert.Equal(HttpStatusCode.OK, rollbackResponse.StatusCode);
        Assert.Equal("Active", await GetStatusAsync(admin, versionOneId, ct));
        Assert.Equal("Inactive", await GetStatusAsync(admin, versionTwoId, ct));
    }

    [Fact]
    public async Task STC006_ActivateVersion_AlreadyActive_Returns422()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var versionId = await RegisterAndActivateAsync(admin, UniqueVersionCode(), ct);

        var secondActivateResponse = await admin.PatchAsync(
            $"/api/v1/ai-model-versions/{versionId}", ToJsonContent(new { status = "ACTIVE" }), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondActivateResponse.StatusCode);
        var body = await secondActivateResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Contains("đang được kích hoạt", body!.Message);
    }

    [Fact]
    public async Task STC007_DoctorViewsActiveVersion_ReturnsReadOnlyShapeWithoutMetrics()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var versionCode = UniqueVersionCode();
        await RegisterAndActivateAsync(admin, versionCode, ct);

        var doctor = await CreateAccountAsync(admin, "STC007 Doctor", "DOCTOR", ct);
        var doctorClient = await LoginAndAuthorizeAsync(app, doctor.Account.PhoneNumber, doctor.TemporaryPassword, ct);

        var response = await doctorClient.GetAsync("/api/v1/ai-model-versions/active", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ActiveAiModelVersionDto>>(ct);
        Assert.Equal(versionCode, body!.Data!.VersionCode);
        Assert.Equal("Active", body.Data.Status);
    }

    // ---- helpers ----

    private static WebApplicationFactory<Program> CreateApp() => new();

    private static string UniqueVersionCode() => "TEST-" + Guid.NewGuid().ToString("N")[..12];

    private static StringContent ToJsonContent(object value) =>
        new(System.Text.Json.JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");

    private static Task<HttpResponseMessage> RegisterVersionAsync(
        HttpClient admin, string versionCode, CancellationToken ct) =>
        admin.PostAsJsonAsync("/api/v1/ai-model-versions", new
        {
            versionCode,
            hfRepoId = RealHfRepoId,
            hfFilename = RealHfFilename,
            description = "System test registration",
        }, ct);

    /// <summary>Đăng ký 1 version mới rồi kích hoạt ngay — gọi THẬT sang AI Python Backend.</summary>
    private static async Task<Guid> RegisterAndActivateAsync(HttpClient admin, string versionCode, CancellationToken ct)
    {
        var registerResponse = await RegisterVersionAsync(admin, versionCode, ct);
        registerResponse.EnsureSuccessStatusCode();
        var registered = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AiModelVersionDto>>(ct);
        var versionId = registered!.Data!.ModelVersionId;

        var activateResponse = await admin.PatchAsync(
            $"/api/v1/ai-model-versions/{versionId}", ToJsonContent(new { status = "ACTIVE" }), ct);
        activateResponse.EnsureSuccessStatusCode();

        return versionId;
    }

    private static async Task<string> GetStatusAsync(HttpClient admin, Guid versionId, CancellationToken ct)
    {
        var response = await admin.GetAsync($"/api/v1/ai-model-versions/{versionId}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiModelVersionDto>>(ct);
        return body!.Data!.Status;
    }

    private static async Task<HttpClient> LoginAsAdminAsync(WebApplicationFactory<Program> app) =>
        await LoginAndAuthorizeAsync(app, SeedAdminPhone, SeedAdminPassword, TestContext.Current.CancellationToken);

    private static async Task<HttpClient> LoginAndAuthorizeAsync(
        WebApplicationFactory<Program> app, string phone, string password, CancellationToken ct)
    {
        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = phone,
            password,
        }, ct);
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    private static string UniquePhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    private static async Task<CreatedUserAccountResponse> CreateAccountAsync(
        HttpClient admin, string fullName, string role, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName,
            role,
        }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(ct);
        return body!.Data!;
    }
}
