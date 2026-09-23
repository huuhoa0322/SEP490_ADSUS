using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.NFR_SecurityAndPerformance;

/// <summary>
/// Report 5.4 — Security (HTTP Flow), grounded in Report 3 §IV.2.3 Quality Attributes
/// (NFR-SEC-01..08), not the generic placeholder the report used before.
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// NFR-SEC-06 (bí mật cấu hình không lộ ra ngoài) và NFR-SEC-08 (bệnh nhân không nhận dữ liệu
/// AI thô) được xác nhận bằng đọc code (AiBackendSettings chỉ đọc qua IOptions, không bao giờ
/// serialize vào DTO nào; PatientCaseResponse là 1 record RIÊNG, không phải CaseResponse với
/// vài field null — trình biên dịch đảm bảo ClinicalInfo/PatientProfile/AiResults không bao
/// giờ khai báo trên đó) — không có case tự động tương ứng, xem Report 5.4.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class SecurityTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly Guid SeedSymptomCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SeedSymptomId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_ADeactivatedAccount_CannotSignIn()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC001 Doctor", "DOCTOR", ct);

        (await admin.PutAsync($"/api/v1/admin/users/{created.Account.UserId}/deactivate", null, ct))
            .EnsureSuccessStatusCode();

        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = created.TemporaryPassword,
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task STC002_ExceedingTheSignInRateLimit_IsRejectedWith429()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var client = app.CreateClient();

        // NFR-SEC-02: 10 request/phút cho mỗi IP nguồn (RateLimitPolicies.Auth, fixed window).
        // WebApplicationFactory dùng chung 1 TestServer trong app này nên cả 11 request cùng
        // rơi vào 1 partition (RemoteIpAddress mặc định của TestServer).
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                phoneNumber = "0999999999",
                password = "wrong-password",
            }, ct);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task STC003_WrongPassword_AndAnUnregisteredPhoneNumber_ProduceTheIdenticalRejection()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC003 Doctor", "DOCTOR", ct);

        var wrongPasswordResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = "definitely-the-wrong-password",
        }, ct);
        var unregisteredPhoneResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = "0888" + Random.Shared.Next(100_000, 999_999),
            password = "anything-at-all",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unregisteredPhoneResponse.StatusCode);
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions, ct);
        var unregisteredPhoneBody = await unregisteredPhoneResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions, ct);
        Assert.Equal(wrongPasswordBody!.Message, unregisteredPhoneBody!.Message);
    }

    [Fact]
    public async Task STC004_AProtectedEndpoint_RejectsAMissingOrInvalidBearerToken()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;

        var noTokenResponse = await app.CreateClient().GetAsync("/api/v1/dashboard/statistics", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, noTokenResponse.StatusCode);

        var garbageTokenClient = app.CreateClient();
        garbageTokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this-is-not-a-real-jwt");
        var garbageTokenResponse = await garbageTokenClient.GetAsync("/api/v1/dashboard/statistics", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, garbageTokenResponse.StatusCode);
    }

    [Fact]
    public async Task STC005_APatientCannotViewAnotherPatientsCase_ByChangingTheCaseIdInTheUrl()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (_, _, caseIdA) = await SetupEndedCaseAsync(app, admin, "STC005 Patient A", ct);
        var doctorB = await CreateDoctorAsync(app, admin, "STC005 Doctor B", ct);
        var (patientB, _) = await CreatePatientWithProfileAsync(app, admin, doctorB, "STC005 Patient B", ct);

        var response = await patientB.GetAsync($"/api/v1/cases/{caseIdA}", ct);

        // GB-05: 404 thay vì 403 — trả 403 cho 1 ca có thật cũng đã gián tiếp xác nhận nó tồn
        // tại, đúng thứ luật này cấm lộ ra.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task STC006_UploadingANonImageFile_AsAnUltrasoundImage_IsRejectedByBothAnalyzeAndConfirm()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC006 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC006 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC006 Staff", ct);
        var caseId = await BookAndCheckInAsync(patient, staff, slotId, "STC006", ct);

        // Nội dung KHÔNG phải JPEG/PNG thật — chỉ đổi tên .jpg, đúng kiểu tấn công NFR-SEC-07
        // được thiết kế để chặn (giả filename/Content-Type, không giả được magic bytes).
        var fakeImageBytes = "this is plain text, not an image"u8.ToArray();

        using var analyzeContent = new MultipartFormDataContent();
        var analyzeFilePart = new ByteArrayContent(fakeImageBytes);
        analyzeFilePart.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        analyzeContent.Add(analyzeFilePart, "Image", "fake.jpg");
        var analyzeResponse = await doctor.PostAsync($"/api/v1/cases/{caseId}/analyze", analyzeContent, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, analyzeResponse.StatusCode);

        using var confirmContent = new MultipartFormDataContent();
        var originalPart = new ByteArrayContent(fakeImageBytes);
        originalPart.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        confirmContent.Add(originalPart, "OriginalImage", "fake-original.jpg");
        var burntPart = new ByteArrayContent(fakeImageBytes);
        burntPart.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        confirmContent.Add(burntPart, "BurntImage", "fake-burnt.jpg");
        confirmContent.Add(new StringContent("[]"), "AiPredictionsJson");
        confirmContent.Add(new StringContent("[]"), "DoctorAnnotationsJson");
        var confirmResponse = await doctor.PostAsync($"/api/v1/cases/{caseId}/images/confirm", confirmContent, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmResponse.StatusCode);
    }

    // ---- shared setup ----

    /// <summary>Đặt lịch có triệu chứng -> check-in -> confirm -> kết thúc (không kê đơn). Trả
    /// về đủ để STC005 test IDOR trên 1 ca đã END — GetForPatientAsync chỉ cho bệnh nhân sở
    /// hữu xem ca đã END, ca Confirmed nhưng chưa kê đơn vẫn ẩn kể cả khi có ID trực tiếp.</summary>
    private static async Task<(HttpClient Doctor, HttpClient Patient, Guid CaseId)> SetupEndedCaseAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string label, CancellationToken ct)
    {
        var doctor = await CreateDoctorAsync(app, admin, $"{label} Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, label, ct);
        var staff = await CreateStaffAsync(app, admin, $"{label} Staff", ct);
        var caseId = await BookAndCheckInAsync(patient, staff, slotId, label, ct);

        (await doctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/confirm", new
        {
            doctorConclusion = $"{label} conclusion",
        }, ct)).EnsureSuccessStatusCode();
        (await doctor.PutAsync($"/api/v1/cases/{caseId}/end", null, ct)).EnsureSuccessStatusCode();

        return (doctor, patient, caseId);
    }

    private static async Task<Guid> BookAndCheckInAsync(
        HttpClient patient, HttpClient staff, Guid slotId, string label, CancellationToken ct)
    {
        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = $"{label} checkup",
            symptoms = new[] { new { categoryId = SeedSymptomCategoryId, symptomId = SeedSymptomId } },
        }, ct);
        bookResponse.EnsureSuccessStatusCode();
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var caseId = booked!.Data!.CaseId!.Value;

        (await staff.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null, ct)).EnsureSuccessStatusCode();
        return caseId;
    }

    // ---- helpers ----

    private static WebApplicationFactory<Program> CreateApp() => new();

    private static string UniquePhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

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
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    private const string FinalTestPassword = "Aa123456@";

    private static async Task<HttpClient> LoginAndForcePasswordChangeAsync(
        WebApplicationFactory<Program> app, string phone, string temporaryPassword, CancellationToken ct)
    {
        var tempClient = await LoginAndAuthorizeAsync(app, phone, temporaryPassword, ct);
        var changeResponse = await tempClient.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            newPassword = FinalTestPassword,
            confirmNewPassword = FinalTestPassword,
        }, ct);
        changeResponse.EnsureSuccessStatusCode();
        var body = await changeResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

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
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(JsonOptions, ct);
        return body!.Data!;
    }

    private static async Task<HttpClient> CreateDoctorAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string fullName, CancellationToken ct)
    {
        var created = await CreateAccountAsync(admin, fullName, "DOCTOR", ct);
        return await LoginAndForcePasswordChangeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
    }

    private static async Task<HttpClient> CreateStaffAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string fullName, CancellationToken ct)
    {
        var created = await CreateAccountAsync(admin, fullName, "STAFF", ct);
        return await LoginAndForcePasswordChangeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
    }

    private static async Task<(HttpClient Client, Guid PatientProfileId)> CreatePatientWithProfileAsync(
        WebApplicationFactory<Program> app, HttpClient admin, HttpClient doctor, string fullName, CancellationToken ct)
    {
        var created = await CreateAccountAsync(admin, fullName, "PATIENT", ct);

        var profileResponse = await doctor.PostAsJsonAsync("/api/v1/patient-profiles", new
        {
            patientUserId = created.Account.UserId,
            gender = "FEMALE",
            diseases = (object?)null,
            allergies = (object?)null,
        }, ct);
        profileResponse.EnsureSuccessStatusCode();
        var profile = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<PatientProfileResponse>>(JsonOptions, ct);

        var client = await LoginAndForcePasswordChangeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
        return (client, profile!.Data!.PatientProfileId);
    }

    private static async Task<Guid> CreateSlotAsync(
        HttpClient doctor, DateOnly visitDate, CancellationToken ct)
    {
        var dayOffsetFromMonday = ((int)visitDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var weekStart = visitDate.AddDays(-dayOffsetFromMonday);

        var ensureResponse = await doctor.PostAsync(
            $"/api/v1/schedule-slots/ensure-default?weekStart={weekStart:yyyy-MM-dd}", null, ct);
        ensureResponse.EnsureSuccessStatusCode();

        var listResponse = await doctor.GetAsync(
            $"/api/v1/schedule-slots?fromDate={visitDate:yyyy-MM-dd}&toDate={visitDate:yyyy-MM-dd}&status=Open", ct);
        listResponse.EnsureSuccessStatusCode();
        var page = await listResponse.Content
            .ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<ScheduleSlotResponse>>>(JsonOptions, ct);
        return page!.Data!.Items[0].SlotId;
    }
}
