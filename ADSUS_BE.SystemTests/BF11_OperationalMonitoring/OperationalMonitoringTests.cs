using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.DashboardReporting.DTOs;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Data;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF11_OperationalMonitoring;

/// <summary>
/// Report 5.3 — BF-11 Operational Monitoring & Audit (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi.
///
/// Vì các bộ đếm thống kê (Dashboard) cộng dồn trên TOÀN BỘ dữ liệu trong DB test (không lọc
/// theo phiên chạy), STC002 dùng assertion CHÊNH LỆCH trước/sau (delta) thay vì so số tuyệt
/// đối — miễn nhiễm với dữ liệu các BF/test case khác để lại.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class OperationalMonitoringTests
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
    public async Task STC001_AdminViewsStatistics_WithNoRange_DefaultsToTheLast30Days_AndNeverErrors()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var defaultResponse = await admin.GetAsync("/api/v1/dashboard/statistics", ct);
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        var defaultBody = await defaultResponse.Content.ReadFromJsonAsync<ApiResponse<DashboardStatisticsResponse>>(JsonOptions, ct);
        var from = DateOnly.ParseExact(defaultBody!.Data!.FromDate, "yyyy-MM-dd");
        var to = DateOnly.ParseExact(defaultBody.Data.ToDate, "yyyy-MM-dd");
        Assert.Equal(29, to.DayNumber - from.DayNumber);

        // AF-01: khoảng ngày sai định dạng bị bỏ qua và rơi về mặc định, KHÔNG báo lỗi.
        var invalidRangeResponse = await admin.GetAsync("/api/v1/dashboard/statistics?fromDate=not-a-date", ct);
        Assert.Equal(HttpStatusCode.OK, invalidRangeResponse.StatusCode);
    }

    [Fact]
    public async Task STC002_CreatingAnAccountToday_IncreasesTodaysNewAccountCount_ByExactlyOne()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        // Dùng đúng lịch phòng khám (UTC+7, xem ClinicClock) chứ không phải ngày UTC thuần —
        // "hôm nay" phía server lệch server 7 tiếng so với UTC nên 2 khái niệm không luôn khớp.
        var today = ClinicClock.Today().ToString("yyyy-MM-dd");

        var before = await GetStatisticsAsync(admin, today, today, ct);
        await CreateAccountAsync(admin, "STC002 New Account", "PATIENT", ct);
        var after = await GetStatisticsAsync(admin, today, today, ct);

        Assert.Equal(before.Accounts.NewInRange + 1, after.Accounts.NewInRange);
    }

    [Fact]
    public async Task STC003_TheStatisticsResponse_NeverExposesAnIndividualPatientsName()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var distinctiveName = $"STC003 Unique Patient {Guid.NewGuid():N}";
        await CreateAccountAsync(admin, distinctiveName, "PATIENT", ct);

        var response = await admin.GetAsync("/api/v1/dashboard/statistics", ct);
        response.EnsureSuccessStatusCode();
        var rawJson = await response.Content.ReadAsStringAsync(ct);

        Assert.DoesNotContain(distinctiveName, rawJson);
    }

    [Fact]
    public async Task STC004_CreatingAnAccount_IsRecordedInTheAuditLog_AndCanBeFoundBySearch()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var uniqueName = $"STC004 Audited Account {Guid.NewGuid():N}";
        await CreateAccountAsync(admin, uniqueName, "DOCTOR", ct);

        var response = await admin.GetAsync($"/api/v1/admin/audit-logs?search={Uri.EscapeDataString(uniqueName)}&page=1", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(JsonOptions, ct);
        var entry = Assert.Single(body!.Data!.Items);
        Assert.Equal("CREATE_ACCOUNT", entry.Action);
        Assert.Contains(uniqueName, entry.Detail);
    }

    [Fact]
    public async Task STC005_FilteringTheAuditLogByAction_OnlyReturnsEntriesOfThatAction()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var uniqueName = $"STC005 Filtered Account {Guid.NewGuid():N}";
        await CreateAccountAsync(admin, uniqueName, "STAFF", ct);

        var matchingResponse = await admin.GetAsync(
            $"/api/v1/admin/audit-logs?search={Uri.EscapeDataString(uniqueName)}&action=CREATE_ACCOUNT&page=1", ct);
        matchingResponse.EnsureSuccessStatusCode();
        var matching = await matchingResponse.Content.ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(JsonOptions, ct);
        Assert.Single(matching!.Data!.Items);

        // Cùng từ khoá tìm kiếm, nhưng lọc theo 1 hành động khác — không được khớp.
        var nonMatchingResponse = await admin.GetAsync(
            $"/api/v1/admin/audit-logs?search={Uri.EscapeDataString(uniqueName)}&action=DEACTIVATE_ACCOUNT&page=1", ct);
        nonMatchingResponse.EnsureSuccessStatusCode();
        var nonMatching = await nonMatchingResponse.Content.ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(JsonOptions, ct);
        Assert.Empty(nonMatching!.Data!.Items);
    }

    [Fact]
    public async Task STC006_ConfirmingACase_IsAClinicalAction_AndIsNotRecordedInTheAuditLog()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC006 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC006 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC006 Staff", ct);
        var uniqueConclusion = $"STC006 unique clinical marker {Guid.NewGuid():N}";

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC006 checkup",
            symptoms = new[] { new { categoryId = SeedSymptomCategoryId, symptomId = SeedSymptomId } },
        }, ct);
        bookResponse.EnsureSuccessStatusCode();
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var caseId = booked!.Data!.CaseId!.Value;

        (await staff.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null, ct)).EnsureSuccessStatusCode();
        (await doctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/confirm", new
        {
            doctorConclusion = uniqueConclusion,
        }, ct)).EnsureSuccessStatusCode();

        var auditResponse = await admin.GetAsync(
            $"/api/v1/admin/audit-logs?search={Uri.EscapeDataString(uniqueConclusion)}&page=1", ct);
        auditResponse.EnsureSuccessStatusCode();
        var audit = await auditResponse.Content.ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(JsonOptions, ct);
        Assert.Empty(audit!.Data!.Items);
    }

    // ---- shared setup ----

    private static async Task<DashboardStatisticsResponse> GetStatisticsAsync(
        HttpClient admin, string fromDate, string toDate, CancellationToken ct)
    {
        var response = await admin.GetAsync($"/api/v1/dashboard/statistics?fromDate={fromDate}&toDate={toDate}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardStatisticsResponse>>(JsonOptions, ct);
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
