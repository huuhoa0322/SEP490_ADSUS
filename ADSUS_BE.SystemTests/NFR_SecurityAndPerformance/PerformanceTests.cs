using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.NFR_SecurityAndPerformance;

/// <summary>
/// Report 5.4 — Performance (HTTP Flow), grounded in Report 3 §IV.2.2 Quality Attributes
/// (NFR-PER-01..05), not the generic placeholder the report used before.
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
///
/// NFR-PER-01 (2 giây, 100 user đồng thời) — chỉ đo độ trễ 1 request đơn lẻ (STC001). Report 3
/// V.3 mục 9 tự nhận "mục tiêu 2 giây / 100 user đồng thời còn là target đến khi đo được trong
/// môi trường thống nhất" — load test 100 user thật cần công cụ riêng (k6...), vượt phạm vi 1
/// System Test HTTP flow, để deferred đúng như Report 3 đã tự ghi nhận.
///
/// NFR-PER-02 (AI/LLM báo tiến trình + hoàn tất/timeout) đã được thực nghiệm gián tiếp qua các
/// lệnh gọi AI Backend/Gemini thật thành công trong BF-02 (activate model) và BF-10 (chatbot) —
/// không có case riêng ở đây.
///
/// NFR-PER-05 (job không tự chồng lấn, lỗi 1 bản ghi không chặn các bản ghi khác) cần 1 bộ khung
/// giả lập scheduler chạy song song thật — để deferred/manual, không cân xứng với 1 request HTTP.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class PerformanceTests
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
    public async Task STC001_AStandardOperation_RespondsWellWithinTheTwoSecondTarget()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var client = app.CreateClient();

        // Lượt đầu tiên của 1 WebApplicationFactory mới dựng phải trả giá khởi động (JIT, dựng
        // EF Core model, mở connection pool...) — chi phí đó không thuộc về NFR-PER-01 (độ trễ
        // vận hành ổn định), nên "làm nóng" 1 lượt bỏ đi trước khi đo, giống connection warm-up
        // thật trong môi trường production luôn chạy sẵn.
        (await client.GetAsync("/api/v1/blog-posts?pageSize=10", ct)).EnsureSuccessStatusCode();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/api/v1/blog-posts?pageSize=10", ct);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Expected under 2000ms, took {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task STC002_RequestingAnOversizedPageSize_IsClampedOnAccountsInvoicesAndAuditLogs()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var usersResponse = await admin.GetAsync("/api/v1/admin/users?pageSize=999999", ct);
        usersResponse.EnsureSuccessStatusCode();
        var users = await usersResponse.Content
            .ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.UserRoleManagement.DTOs.PagedResult<UserAccountResponse>>>(JsonOptions, ct);
        Assert.True(users!.Data!.PageSize <= 100, $"Accounts pageSize was not clamped: {users.Data.PageSize}.");

        // InvoicesController yêu cầu STAFF hoặc DOCTOR — Admin không có quyền, phải dùng tài
        // khoản Staff riêng, không dùng chung client admin như 2 endpoint còn lại.
        var staff = await CreateStaffAsync(app, admin, "STC002 Staff", ct);
        var invoicesResponse = await staff.GetAsync("/api/v1/invoices?pageSize=999999", ct);
        invoicesResponse.EnsureSuccessStatusCode();
        var invoices = await invoicesResponse.Content
            .ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<InvoiceResponse>>>(JsonOptions, ct);
        Assert.True(invoices!.Data!.PageSize <= 100, $"Invoices pageSize was not clamped: {invoices.Data.PageSize}.");

        var auditResponse = await admin.GetAsync("/api/v1/admin/audit-logs?pageSize=999999&page=1", ct);
        auditResponse.EnsureSuccessStatusCode();
        var audit = await auditResponse.Content
            .ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<AuditLogResponse>>>(JsonOptions, ct);
        Assert.True(audit!.Data!.PageSize <= 100, $"Audit log pageSize was not clamped: {audit.Data.PageSize}.");
    }

    [Fact]
    public async Task STC003_AnUltrasoundImageOverTwentyMebibytes_IsRejectedBeforeReachingAiAnalysis()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC003 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC003 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC003 Staff", ct);
        var caseId = await BookAndCheckInAsync(patient, staff, slotId, "STC003", ct);

        // 20 MiB + 1 byte — không cần nội dung ảnh thật, kiểm tra kích thước chặn trước cả
        // bước đọc magic bytes (CaseDiagnosisController.AnalyzeImage kiểm tra Length trước).
        var oversizedBytes = new byte[20 * 1024 * 1024 + 1];
        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(oversizedBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(filePart, "Image", "oversized.jpg");

        var response = await doctor.PostAsync($"/api/v1/cases/{caseId}/analyze", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- shared setup ----

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
