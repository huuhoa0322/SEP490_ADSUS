using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF08_DailyHealthTracking;

/// <summary>
/// Report 5.3 — BF-08 Daily Health Tracking (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi. CỐ Ý bỏ qua JOB-04 (nhắc thiếu nhật ký lúc 08h/20h) và JOB-05
/// (nhắc đánh giá sức khỏe hàng tuần) — cả 2 phụ thuộc lịch chạy nền thật, không kiểm được qua
/// 1 request HTTP.
///
/// LỆCH SO VỚI TÀI LIỆU: Report 3 mô tả GET có "date and type filters", nhưng đọc thẳng
/// HealthLogsController/HealthLogSearchCriteria xác nhận chỉ có filter theo NGÀY — không có
/// tham số lọc theo loại (Exercise/Diet); client phải tự lọc phía FE trên kết quả 1 ngày. Test
/// bám theo hành vi thật của code.
///
/// Không có endpoint nào cho phép truyền patientId của người khác (khác hẳn Prescription/
/// MedicationIntake ở BF-06) — PatientProfileId luôn tự suy ra từ JWT, nên không có tình huống
/// "cố xem log người khác" để gọi API; STC005 thay vào đó xác nhận Patient B chỉ thấy log của
/// chính mình dù Patient A đã ghi log cùng ngày.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class DailyHealthTrackingTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_PatientLogsAnExerciseEntry_ForAGivenDay()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC001 Doctor", ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC001 Patient", ct);
        var logDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await patient.PostAsJsonAsync("/api/v1/health-logs", new
        {
            type = "EXERCISE",
            content = "STC001 30 minute walk",
            logDate,
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<HealthLogResponse>>(JsonOptions, ct);
        Assert.Equal("EXERCISE", body!.Data!.Type);
        Assert.Equal("STC001 30 minute walk", body.Data.Content);
        Assert.Equal(logDate, body.Data.LogDate);
    }

    [Fact]
    public async Task STC002_LoggingMultipleEntriesForTheSameDay_AccumulatesInsteadOfOverwriting()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC002 Doctor", ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC002 Patient", ct);
        var logDate = DateOnly.FromDateTime(DateTime.UtcNow);

        (await LogHealthDataAsync(patient, "EXERCISE", "STC002 morning run", logDate, ct)).EnsureSuccessStatusCode();
        (await LogHealthDataAsync(patient, "DIET", "STC002 salad for lunch", logDate, ct)).EnsureSuccessStatusCode();

        var logs = await GetHealthLogsAsync(patient, logDate, ct);
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.Type == "EXERCISE" && l.Content == "STC002 morning run");
        Assert.Contains(logs, l => l.Type == "DIET" && l.Content == "STC002 salad for lunch");
    }

    [Fact]
    public async Task STC003_LoggingWithAnInvalidType_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC003 Doctor", ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC003 Patient", ct);

        var response = await LogHealthDataAsync(patient, "SLEEP", "STC003 not a supported type", null, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task STC004_ReviewingLogsForASpecificDate_ExcludesEntriesLoggedForAnotherDay()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC004 Doctor", ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC004 Patient", ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        (await LogHealthDataAsync(patient, "EXERCISE", "STC004 today's entry", today, ct)).EnsureSuccessStatusCode();
        (await LogHealthDataAsync(patient, "EXERCISE", "STC004 yesterday's entry", yesterday, ct)).EnsureSuccessStatusCode();

        var todayLogs = await GetHealthLogsAsync(patient, today, ct);
        Assert.Single(todayLogs, l => l.Content == "STC004 today's entry");

        var yesterdayLogs = await GetHealthLogsAsync(patient, yesterday, ct);
        Assert.Single(yesterdayLogs, l => l.Content == "STC004 yesterday's entry");
    }

    [Fact]
    public async Task STC005_APatientsLogReview_NeverIncludesAnotherPatientsEntries()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC005 Doctor", ct);
        var (patientA, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC005 Patient A", ct);
        var (patientB, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC005 Patient B", ct);
        var logDate = DateOnly.FromDateTime(DateTime.UtcNow);

        (await LogHealthDataAsync(patientA, "DIET", "STC005 Patient A's private entry", logDate, ct)).EnsureSuccessStatusCode();

        var patientBLogs = await GetHealthLogsAsync(patientB, logDate, ct);
        Assert.Empty(patientBLogs);
    }

    // ---- shared setup ----

    private static Task<HttpResponseMessage> LogHealthDataAsync(
        HttpClient patient, string type, string content, DateOnly? logDate, CancellationToken ct) =>
        patient.PostAsJsonAsync("/api/v1/health-logs", new
        {
            type,
            content,
            logDate,
        }, ct);

    private static async Task<List<HealthLogResponse>> GetHealthLogsAsync(
        HttpClient patient, DateOnly date, CancellationToken ct)
    {
        var response = await patient.GetAsync($"/api/v1/health-logs?date={date:yyyy-MM-dd}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<HealthLogResponse>>>(JsonOptions, ct);
        return body!.Data!;
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

    /// <summary>Đăng nhập bằng mật khẩu tạm do Admin cấp, rồi đổi ngay sang mật khẩu cố định.
    /// MustChangePasswordMiddleware chặn (403) mọi request khác ngoài change-password/logout khi
    /// access token còn mang claim MustChangePassword=true.</summary>
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
}
