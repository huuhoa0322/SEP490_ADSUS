using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.CaseClinicServices.DTOs;
using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF04_ClinicalVisit;

/// <summary>
/// Report 5.3 — BF-04 AI-Assisted Clinical Visit & Feedback (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi, mỗi case kiểm 1 quy tắc nghiệp vụ khác nhau. CỐ Ý bỏ qua
/// bước phân tích ảnh AI thật (UC-25 AnalyzeImage/ConfirmAnalysis) — cần upload ảnh siêu âm
/// thật + gọi AI Python Backend chạy inference, quá nặng và không cần thiết cho việc verify
/// vòng đời Case (SaveConclusion/Confirm/End không hề yêu cầu đã có kết quả AI, đọc thẳng từ
/// CasesController.cs xác nhận điều này).
///
/// Cần sẵn:
/// - 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// - 1 symptom category + 1 symptom (bảng symptom_categories/symptoms) đã seed thủ công qua
///   SQL — API không có endpoint tạo dữ liệu danh mục này, seed 1 lần cho môi trường test.
///   ID cố định dùng trong test: xem SeedSymptomCategoryId/SeedSymptomId bên dưới.
/// </summary>
public class ClinicalVisitTests
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
    public async Task STC001_BookingWithSymptoms_CreatesCase_CheckinMovesItToInProgress()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC001 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC001 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC001 Staff", ct);

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC001 checkup",
            symptoms = new[] { new { categoryId = SeedSymptomCategoryId, symptomId = SeedSymptomId } },
        }, ct);
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var caseId = booked!.Data!.CaseId
            ?? throw new InvalidOperationException("Booking with symptoms did not create a Case.");

        var checkinResponse = await staff.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null, ct);
        Assert.Equal(HttpStatusCode.OK, checkinResponse.StatusCode);

        var caseResponse = await doctor.GetAsync($"/api/v1/cases/{caseId}", ct);
        var caseBody = await caseResponse.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>(JsonOptions, ct);
        Assert.Equal("IN_PROGRESS", caseBody!.Data!.Status);
    }

    [Fact]
    public async Task STC002_ResponsibleDoctorSavesConclusion_DraftDoesNotChangeStatus()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, doctor, caseId) = await SetupCheckedInCaseAsync(app, "STC002", ct);

        var response = await doctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/conclusion", new
        {
            doctorConclusion = "STC002 draft conclusion, not final yet",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>(JsonOptions, ct);
        Assert.Equal("IN_PROGRESS", body!.Data!.Status);
    }

    [Fact]
    public async Task STC003_DoctorNotResponsibleForCase_CannotConfirmIt()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, _, caseId) = await SetupCheckedInCaseAsync(app, "STC003", ct);
        var otherDoctor = await CreateDoctorAsync(app, admin, "STC003 Other Doctor", ct);

        var response = await otherDoctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/confirm", new
        {
            doctorConclusion = "STC003 attempted by the wrong doctor",
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions, ct);
        Assert.Contains("responsible doctor", body!.Message);
    }

    [Fact]
    public async Task STC004_ResponsibleDoctorConfirmsCase_StatusBecomesConfirmed()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, doctor, caseId) = await SetupCheckedInCaseAsync(app, "STC004", ct);

        var response = await doctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/confirm", new
        {
            doctorConclusion = "STC004 final diagnostic conclusion",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>(JsonOptions, ct);
        Assert.Equal("CONFIRMED", body!.Data!.Status);
    }

    [Fact]
    public async Task STC005_DoctorAddsClinicServiceToCase_ServiceIsListedForTheCase()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, doctor, caseId) = await SetupCheckedInCaseAsync(app, "STC005", ct);
        var clinicServiceId = await CreateClinicServiceAsync(admin, ct);

        var response = await doctor.PostAsJsonAsync($"/api/v1/cases/{caseId}/services", new
        {
            clinicServiceId,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listResponse = await doctor.GetAsync($"/api/v1/cases/{caseId}/services", ct);
        var list = await listResponse.Content
            .ReadFromJsonAsync<ApiResponse<List<CaseClinicServiceResponse>>>(JsonOptions, ct);
        Assert.Contains(list!.Data!, s => s.ClinicServiceId == clinicServiceId);
    }

    [Fact]
    public async Task STC006_DoctorEndsConfirmedCaseWithoutPrescription_StatusBecomesEnd()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, doctor, caseId) = await SetupCheckedInCaseAsync(app, "STC006", ct);

        var confirmResponse = await doctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/confirm", new
        {
            doctorConclusion = "STC006 conclusion before ending without a prescription",
        }, ct);
        confirmResponse.EnsureSuccessStatusCode();

        var endResponse = await doctor.PutAsync($"/api/v1/cases/{caseId}/end", null, ct);

        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
        var body = await endResponse.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>(JsonOptions, ct);
        Assert.Equal("END", body!.Data!.Status);
    }

    [Fact]
    public async Task STC007_PatientViewsOwnEndedCase_StaffAlsoSeesItAsEnd()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, doctor, caseId, patient) = await SetupEndedCaseAsync(app, "STC007", ct);

        var patientResponse = await patient.GetAsync($"/api/v1/cases/{caseId}", ct);
        Assert.Equal(HttpStatusCode.OK, patientResponse.StatusCode);
        var patientBody = await patientResponse.Content.ReadFromJsonAsync<ApiResponse<PatientCaseResponse>>(JsonOptions, ct);
        Assert.Equal("END", patientBody!.Data!.Status);

        var staffResponse = await doctor.GetAsync($"/api/v1/cases/{caseId}", ct);
        var staffBody = await staffResponse.Content.ReadFromJsonAsync<ApiResponse<CaseResponse>>(JsonOptions, ct);
        Assert.Equal("END", staffBody!.Data!.Status);
    }

    [Fact]
    public async Task STC008_PatientSubmitsFeedback_AdminCanSeeItInTheFeedbackList()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var (admin, _, caseId, patient) = await SetupEndedCaseAsync(app, "STC008", ct);

        var feedbackResponse = await patient.PostAsJsonAsync($"/api/v1/me/case-feedbacks?caseId={caseId}", new
        {
            rating = 5,
            content = "STC008 great visit, thank you",
        }, ct);
        Assert.Equal(HttpStatusCode.Created, feedbackResponse.StatusCode);

        var adminListResponse = await admin.GetAsync("/api/v1/admin/feedbacks?pageSize=100", ct);
        Assert.Equal(HttpStatusCode.OK, adminListResponse.StatusCode);
        var adminList = await adminListResponse.Content
            .ReadFromJsonAsync<ApiResponse<ADSUS_BE.BLL.Common.PagedResult<FeedbackResponse>>>(JsonOptions, ct);
        Assert.Contains(adminList!.Data!.Items, f => f.CaseId == caseId);
    }

    // ---- shared setup ----

    /// <summary>Đặt lịch (có triệu chứng) + check-in — Case ở trạng thái InProgress, sẵn sàng
    /// cho các thao tác của Bác sĩ (conclusion/confirm/services).</summary>
    private static async Task<(HttpClient Admin, HttpClient Doctor, Guid CaseId)> SetupCheckedInCaseAsync(
        WebApplicationFactory<Program> app, string label, CancellationToken ct)
    {
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, $"{label} Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, $"{label} Patient", ct);
        var staff = await CreateStaffAsync(app, admin, $"{label} Staff", ct);

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = $"{label} checkup",
            symptoms = new[] { new { categoryId = SeedSymptomCategoryId, symptomId = SeedSymptomId } },
        }, ct);
        bookResponse.EnsureSuccessStatusCode();
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var caseId = booked!.Data!.CaseId!.Value;

        var checkinResponse = await staff.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null, ct);
        checkinResponse.EnsureSuccessStatusCode();

        return (admin, doctor, caseId);
    }

    /// <summary>Case InProgress -> Confirmed -> End (không kê đơn), kèm client của Patient sở
    /// hữu ca đó — dùng cho các case cần 1 ca đã hoàn tất.</summary>
    private static async Task<(HttpClient Admin, HttpClient Doctor, Guid CaseId, HttpClient Patient)> SetupEndedCaseAsync(
        WebApplicationFactory<Program> app, string label, CancellationToken ct)
    {
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, $"{label} Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, $"{label} Patient", ct);
        var staff = await CreateStaffAsync(app, admin, $"{label} Staff", ct);

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
        (await doctor.PutAsJsonAsync($"/api/v1/cases/{caseId}/confirm", new
        {
            doctorConclusion = $"{label} conclusion",
        }, ct)).EnsureSuccessStatusCode();
        (await doctor.PutAsync($"/api/v1/cases/{caseId}/end", null, ct)).EnsureSuccessStatusCode();

        return (admin, doctor, caseId, patient);
    }

    // ---- helpers ----

    private static WebApplicationFactory<Program> CreateApp() => new();

    private static string UniquePhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    private static string UniqueCode() => "TEST-" + Guid.NewGuid().ToString("N")[..12];

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
    /// access token còn mang claim MustChangePassword=true. Sau khi vá AuthService.ChangePasswordAsync
    /// (21/09/2026) để phát token mới ngay trong response — cùng cách LoginAsync phát token — chỉ
    /// cần 1 vòng đăng nhập, không cần đăng nhập lại lần 2 như trước.</summary>
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

    /// <summary>Không còn endpoint tạo 1 slot thủ công (POST /api/v1/schedule-slots đã bị gỡ khỏi
    /// ScheduleSlotsController trong lần merge master gần đây) — slot giờ chỉ sinh được qua
    /// ensure-default (tự sinh nguyên tuần T2-CN, 16 ca 30 phút/ngày, idempotent), rồi lấy 1 ca
    /// Open trong ngày cần dùng qua GET danh sách.</summary>
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

    private static async Task<Guid> CreateClinicServiceAsync(HttpClient admin, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/clinic-services", new
        {
            code = UniqueCode(),
            name = "System test service",
            description = "Created by BF-04 System Test",
            price = 100000,
        }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClinicServiceResponse>>(JsonOptions, ct);
        return body!.Data!.Id;
    }
}
