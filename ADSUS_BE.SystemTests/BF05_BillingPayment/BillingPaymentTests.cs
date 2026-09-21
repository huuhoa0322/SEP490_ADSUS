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
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF05_BillingPayment;

/// <summary>
/// Report 5.3 — BF-05 Billing, Payment & Medicine Dispensing (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// CỐ Ý chỉ dùng Case có DỊCH VỤ PHÒNG KHÁM (không có đơn thuốc) — InvoiceService xác nhận
/// dispense thuốc chỉ chạy "if (hasPrescription)" (PayAndDispenseAsync), nên nhánh dịch vụ đủ
/// để verify toàn bộ vòng đời Invoice (generate/pay/cancel/chặn xoá) mà không cần seed danh
/// mục thuốc + tồn kho (medicines/medicine_batch) — nặng hơn nhiều so với 1 symptom category
/// đã seed cho BF-04. Việc dispense thuốc thật (FEFO) để lại cho BF-06/BF-07.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01).
/// </summary>
public class BillingPaymentTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_AdminCreatesClinicService_Returns201WithSubmittedPrice()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/clinic-services", new
        {
            code = UniqueCode(),
            name = "STC001 Service",
            description = "STC001 catalog entry",
            price = 150000,
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClinicServiceResponse>>(JsonOptions, ct);
        Assert.Equal(150000, body!.Data!.Price);
        Assert.True(body.Data.IsActive);
    }

    [Fact]
    public async Task STC002_EndingCaseWithBilledService_AutoGeneratesPendingInvoice_StaffCanViewItItemized()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, caseId) = await SetupCaseWithBilledServiceAsync(app, admin, "STC002", ct);

        var endResponse = await doctor.PutAsync($"/api/v1/cases/{caseId}/end", null, ct);
        endResponse.EnsureSuccessStatusCode();

        // GenerateInvoiceForCase là idempotent (trả lại hóa đơn đã có nếu PENDING/PAID) — dùng
        // để lấy đúng invoiceId đã tự sinh lúc End, không tạo hóa đơn thứ hai.
        var invoiceId = await doctor.PostAsync($"/api/v1/invoices/generate/{caseId}", null, ct)
            is var generateResponse && generateResponse.IsSuccessStatusCode
                ? await generateResponse.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct)
                : throw new InvalidOperationException("Failed to look up the auto-generated invoice.");

        var detailResponse = await doctor.GetAsync($"/api/v1/invoices/{invoiceId}", ct);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResponse<InvoiceDetailResponse>>(JsonOptions, ct);
        Assert.Equal("PENDING", detail!.Data!.Status);
        Assert.Single(detail.Data.Items);
        Assert.Equal("SERVICE", detail.Data.Items[0].ItemType);
    }

    [Fact]
    public async Task STC003_PayingInvoice_MovesItToPaid_RemovingServiceAfterwardIsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, caseId, caseClinicServiceId) = await SetupConfirmedCaseWithBilledServiceAsync(app, admin, "STC003", ct);

        var generateResponse = await doctor.PostAsync($"/api/v1/invoices/generate/{caseId}", null, ct);
        generateResponse.EnsureSuccessStatusCode();
        var invoiceId = await generateResponse.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var payResponse = await doctor.PutAsJsonAsync($"/api/v1/invoices/{invoiceId}/pay", new
        {
            paymentMethod = "CASH",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

        var detailResponse = await doctor.GetAsync($"/api/v1/invoices/{invoiceId}", ct);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResponse<InvoiceDetailResponse>>(JsonOptions, ct);
        Assert.Equal("PAID", detail!.Data!.Status);

        // Ca vẫn đang Confirmed (chưa End) tại thời điểm này, nên chặn xoá dịch vụ ở đây chắc
        // chắn đến từ luật "hóa đơn đã thanh toán", không lẫn với luật "ca đã kết thúc".
        var removeResponse = await doctor.DeleteAsync($"/api/v1/cases/{caseId}/services/{caseClinicServiceId}", ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, removeResponse.StatusCode);
        var removeBody = await removeResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions, ct);
        Assert.Contains("thanh toán", removeBody!.Message);
    }

    [Fact]
    public async Task STC004_CancellingPendingInvoiceWithReason_Succeeds()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, caseId) = await SetupCaseWithBilledServiceAsync(app, admin, "STC004", ct);

        var generateResponse = await doctor.PostAsync($"/api/v1/invoices/generate/{caseId}", null, ct);
        generateResponse.EnsureSuccessStatusCode();
        var invoiceId = await generateResponse.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var cancelResponse = await doctor.PutAsJsonAsync($"/api/v1/invoices/{invoiceId}/cancel", new
        {
            reason = "STC004 patient changed their mind before paying",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var detailResponse = await doctor.GetAsync($"/api/v1/invoices/{invoiceId}", ct);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResponse<InvoiceDetailResponse>>(JsonOptions, ct);
        Assert.Equal("CANCELLED", detail!.Data!.Status);
    }

    [Fact]
    public async Task STC005_GeneratingInvoice_ForCaseWithNothingBilled_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC005 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC005 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC005 Staff", ct);

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC005 checkup, nothing ever gets billed",
            symptoms = new[] { new { categoryId = SeedSymptomCategoryId, symptomId = SeedSymptomId } },
        }, ct);
        bookResponse.EnsureSuccessStatusCode();
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var caseId = booked!.Data!.CaseId!.Value;

        (await staff.PostAsync($"/api/v1/cases/{caseId}/appointment/checkin", null, ct)).EnsureSuccessStatusCode();

        // Ca đã tồn tại (đã check-in) nhưng chưa được thêm dịch vụ hay đơn thuốc nào — đúng
        // nhánh "Case tồn tại nhưng không có gì để tính tiền" trong GenerateInvoiceForCaseAsync.
        var response = await doctor.PostAsync($"/api/v1/invoices/generate/{caseId}", null, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---- shared setup ----

    /// <summary>Đặt lịch (không cần triệu chứng) -> check-in qua appointmentId -> KHÔNG có Case.
    /// Dùng khi chỉ cần verify hành vi ở tầng Invoice, không cần Case thật.</summary>
    private static async Task<(HttpClient Doctor, Guid CaseId)> SetupCaseWithBilledServiceAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string label, CancellationToken ct)
    {
        var (doctor, caseId, _) = await SetupConfirmedCaseWithBilledServiceAsync(app, admin, label, ct);
        return (doctor, caseId);
    }

    /// <summary>Đặt lịch có triệu chứng (tạo Case) -> check-in -> confirm -> thêm 1 dịch vụ
    /// phòng khám. Case dừng ở Confirmed (CHƯA End) để các test Invoice không lẫn với luật
    /// "ca đã kết thúc" của CaseClinicServiceService.</summary>
    private static async Task<(HttpClient Doctor, Guid CaseId, Guid CaseClinicServiceId)> SetupConfirmedCaseWithBilledServiceAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string label, CancellationToken ct)
    {
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

        var clinicServiceId = await CreateClinicServiceAsync(admin, ct);
        var addServiceResponse = await doctor.PostAsJsonAsync($"/api/v1/cases/{caseId}/services", new
        {
            clinicServiceId,
        }, ct);
        addServiceResponse.EnsureSuccessStatusCode();
        var addServiceBody = await addServiceResponse.Content
            .ReadFromJsonAsync<ApiResponse<CaseClinicServiceResponse>>(JsonOptions, ct);

        return (doctor, caseId, addServiceBody!.Data!.Id);
    }

    private static readonly Guid SeedSymptomCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SeedSymptomId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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
        return await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
    }

    private static async Task<HttpClient> CreateStaffAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string fullName, CancellationToken ct)
    {
        var created = await CreateAccountAsync(admin, fullName, "STAFF", ct);
        return await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
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

        var client = await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword, ct);
        return (client, profile!.Data!.PatientProfileId);
    }

    private static async Task<Guid> CreateSlotAsync(
        HttpClient doctor, DateOnly visitDate, CancellationToken ct,
        string startTime = "09:00:00", string endTime = "10:00:00")
    {
        var response = await doctor.PostAsJsonAsync("/api/v1/schedule-slots", new
        {
            visitDate,
            startTime,
            endTime,
        }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScheduleSlotResponse>>(JsonOptions, ct);
        return body!.Data!.SlotId;
    }

    private static async Task<Guid> CreateClinicServiceAsync(HttpClient admin, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/clinic-services", new
        {
            code = UniqueCode(),
            name = "System test service",
            description = "Created by BF-05 System Test",
            price = 100000,
        }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClinicServiceResponse>>(JsonOptions, ct);
        return body!.Data!.Id;
    }
}
