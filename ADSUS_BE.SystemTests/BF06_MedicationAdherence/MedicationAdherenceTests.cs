using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ADSUS_BE.SystemTests.BF06_MedicationAdherence;

/// <summary>
/// Report 5.3 — BF-06 Medication Adherence Monitoring (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi, mỗi case kiểm 1 quy tắc nghiệp vụ khác nhau.
///
/// LỆCH SO VỚI TÀI LIỆU (Report 3, 2. Main Business Flow §2.5/§2.6): tài liệu mô tả dispense
/// thuốc + tạo intake log xảy ra SAU KHI Invoice PAID. Đọc thẳng InvoiceService.cs xác nhận
/// thực tế KHÁC: GenerateInvoiceForCaseAsync (chạy ngay khi kê đơn, lúc Invoice còn PENDING) đã
/// gọi DispenseAsync + tạo intake log ngay tại đó; PayInvoiceAsync (endpoint "pay") chỉ đổi
/// status, không dispense gì thêm. Đã xác nhận CancelInvoiceAsync hoàn kho/xoá intake log đúng
/// cho cả 2 trạng thái PENDING/PAID nên đây không phải rò rỉ tồn kho — chỉ là mốc thời điểm
/// dispense khác tài liệu. Theo yêu cầu người dùng, test bám theo HÀNH VI THẬT của code, không
/// theo tài liệu (phát hiện 21/09/2026).
///
/// Luật 2 tiếng (MedicationIntakeService.ConfirmTakenAsync) không thể tái hiện tự nhiên trong 1
/// lần chạy test: MedicationIntakeScheduleGenerator luôn bỏ qua slot đã quá giờ của ngày 0, nên
/// liều đầu tiên luôn nằm trong tương lai ngay sau khi tạo. Dùng trực tiếp AppDbContext (cùng 1
/// DB test thật, không phải service giả) để lùi ScheduledTime của 1 log đã tồn tại về quá khứ —
/// tương đương backdate dữ liệu setup, không phải mock logic đang được kiểm thử.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01), và bảng medicine_unit đã
/// seed 1 dòng "Viên" (giống symptom_categories/symptoms ở BF-04 — bảng tra cứu trống, không có
/// API tạo).
/// </summary>
public class MedicationAdherenceTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly Guid SeedSymptomCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SeedSymptomId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SeedMedicineUnitId = Guid.Parse("7b090445-b234-441b-a384-33b878cb831c");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_DoctorPrescribesActiveMedicineForConfirmedCase_DispensesAndSchedulesIntakeDoses()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, patient, _, caseId, prescriptionId) =
            await SetupPrescribedCaseAsync(app, admin, "STC001", ct, durationDays: 3);

        // UC-27: Doctor xem đơn vừa kê qua case detail.
        var caseRxResponse = await doctor.GetAsync($"/api/v1/cases/{caseId}/prescription", ct);
        Assert.Equal(HttpStatusCode.OK, caseRxResponse.StatusCode);
        var caseRx = await caseRxResponse.Content.ReadFromJsonAsync<ApiResponse<PrescriptionResponse>>(JsonOptions, ct);
        Assert.Equal(prescriptionId, caseRx!.Data!.PrescriptionId);

        // Đơn dùng cả 3 khung uống x 3 ngày -> đúng 9 liều được sinh sẵn (bất kể giờ chạy test
        // trong ngày — slot quá khứ của ngày 0 chỉ bị đẩy sang ngày kế tiếp, không giảm tổng số).
        var intakesResponse = await patient.GetAsync($"/api/v1/me/medication-intakes/prescription/{prescriptionId}", ct);
        Assert.Equal(HttpStatusCode.OK, intakesResponse.StatusCode);
        var intakes = await intakesResponse.Content.ReadFromJsonAsync<ApiResponse<List<IntakeLogResponse>>>(JsonOptions, ct);
        Assert.Equal(9, intakes!.Data!.Count);
        Assert.All(intakes.Data, i => Assert.Equal("PENDING", i.Status));
    }

    [Fact]
    public async Task STC002_DoctorPrescribesInactiveMedicine_Returns422()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, _, _, caseId) = await SetupConfirmedCaseAsync(app, admin, "STC002", ct);
        var (medicineId, medicineName) = await CreateActiveMedicineWithStockAsync(admin, "STC002", ct);

        (await admin.DeleteAsync($"/api/v1/medicines/{medicineId}", ct)).EnsureSuccessStatusCode();

        var response = await PrescribeAsync(doctor, caseId, medicineName, 3, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task STC003_PatientConfiguresReminderPreferences_PersistsSubmittedTimes()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC003 Doctor", ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC003 Patient", ct);

        var upsertResponse = await patient.PutAsJsonAsync("/api/v1/me/reminder-preference", new
        {
            notifEnabled = true,
            morningTime = "08:00",
            middayTime = "13:00",
            eveningTime = "21:00",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        var getResponse = await patient.GetAsync("/api/v1/me/reminder-preference", ct);
        var pref = await getResponse.Content.ReadFromJsonAsync<ApiResponse<ReminderPreferenceResponse>>(JsonOptions, ct);
        Assert.Equal("08:00", pref!.Data!.MorningTime);
        Assert.Equal("13:00", pref.Data.MiddayTime);
        Assert.Equal("21:00", pref.Data.EveningTime);
    }

    [Fact]
    public async Task STC004_PatientConfirmsIntake_BeforeScheduledTime_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (_, patient, _, _, prescriptionId) =
            await SetupPrescribedCaseAsync(app, admin, "STC004", ct, durationDays: 1);

        var intakeId = await GetFirstIntakeIdAsync(patient, prescriptionId, ct);

        var confirmResponse = await patient.PostAsync($"/api/v1/me/medication-intakes/{intakeId}/confirm", null, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmResponse.StatusCode);
        var body = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions, ct);
        Assert.Contains("Chưa đến giờ", body!.Message);
    }

    [Fact]
    public async Task STC005_PatientConfirmsIntake_WithinAllowedWindow_Succeeds_AndConfirmingAgainIsIdempotent()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (_, patient, _, _, prescriptionId) =
            await SetupPrescribedCaseAsync(app, admin, "STC005", ct, durationDays: 1);

        var intakeId = await GetFirstIntakeIdAsync(patient, prescriptionId, ct);
        await BackdateIntakeAsync(app, intakeId, TimeSpan.FromMinutes(-30), ct);

        var firstConfirm = await patient.PostAsync($"/api/v1/me/medication-intakes/{intakeId}/confirm", null, ct);
        Assert.Equal(HttpStatusCode.OK, firstConfirm.StatusCode);

        // GB-01: xác nhận lại lần 2 vẫn phải trả 200 (idempotent), không được lỗi.
        var secondConfirm = await patient.PostAsync($"/api/v1/me/medication-intakes/{intakeId}/confirm", null, ct);
        Assert.Equal(HttpStatusCode.OK, secondConfirm.StatusCode);

        var intakesResponse = await patient.GetAsync($"/api/v1/me/medication-intakes/prescription/{prescriptionId}", ct);
        var intakes = await intakesResponse.Content.ReadFromJsonAsync<ApiResponse<List<IntakeLogResponse>>>(JsonOptions, ct);
        Assert.Equal("TAKEN", intakes!.Data!.Single(i => i.IntakeId == intakeId).Status);
    }

    [Fact]
    public async Task STC006_PatientConfirmsIntake_MoreThanTwoHoursLate_IsRejected_DoctorCanStillSendManualReminder()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, patient, patientProfileId, _, prescriptionId) =
            await SetupPrescribedCaseAsync(app, admin, "STC006", ct, durationDays: 1);

        var intakeId = await GetFirstIntakeIdAsync(patient, prescriptionId, ct);
        await BackdateIntakeAsync(app, intakeId, TimeSpan.FromHours(-3), ct);

        var confirmResponse = await patient.PostAsync($"/api/v1/me/medication-intakes/{intakeId}/confirm", null, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmResponse.StatusCode);
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions, ct);
        Assert.Contains("quá 2 tiếng", confirmBody!.Message);

        // Liều quá hạn nhưng CHƯA uống — bác sĩ vẫn nhắc thủ công được (UC-30).
        var remindResponse = await doctor.PostAsJsonAsync(
            $"/api/v1/me/medication-tracking/patients/{patientProfileId}/remind",
            new { prescriptionId }, ct);
        Assert.Equal(HttpStatusCode.OK, remindResponse.StatusCode);
        var remindBody = await remindResponse.Content.ReadFromJsonAsync<ApiResponse<RemindResponse>>(JsonOptions, ct);
        Assert.Equal(1, remindBody!.Data!.SentCount);
    }

    [Fact]
    public async Task STC007_PatientCannotViewAnotherPatientsPrescriptionIntakes()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, _, _, _, prescriptionId) =
            await SetupPrescribedCaseAsync(app, admin, "STC007", ct, durationDays: 1);
        var (strangerPatient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC007 Stranger", ct);

        var response = await strangerPatient.GetAsync($"/api/v1/me/medication-intakes/prescription/{prescriptionId}", ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task STC008_DoctorReviewsPatientAdherenceList_AndPrescriptionDetail()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (doctor, _, patientProfileId, _, prescriptionId) =
            await SetupPrescribedCaseAsync(app, admin, "STC008", ct, durationDays: 1);

        var listResponse = await doctor.GetAsync("/api/v1/me/medication-tracking/patients", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<DoctorPatientListResponse>>(JsonOptions, ct);
        var patientEntry = list!.Data!.Patients.Single(p => p.PatientProfileId == patientProfileId);
        Assert.Equal(1, patientEntry.ActivePrescriptionCount);

        var detailResponse = await doctor.GetAsync($"/api/v1/me/medication-tracking/patients/{patientProfileId}/prescriptions", ct);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResponse<PatientPrescriptionDetailResponse>>(JsonOptions, ct);
        Assert.Contains(detail!.Data!.Prescriptions, p => p.PrescriptionId == prescriptionId);
    }

    // ---- shared setup ----

    /// <summary>Đặt lịch có triệu chứng (tạo Case) -> check-in -> confirm. Case dừng ở Confirmed,
    /// sẵn sàng để kê đơn (UC-18 BR-01: chỉ ca Confirmed mới được kê đơn).</summary>
    private static async Task<(HttpClient Doctor, HttpClient Patient, Guid PatientProfileId, Guid CaseId)> SetupConfirmedCaseAsync(
        WebApplicationFactory<Program> app, HttpClient admin, string label, CancellationToken ct)
    {
        var doctor = await CreateDoctorAsync(app, admin, $"{label} Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, patientProfileId) = await CreatePatientWithProfileAsync(app, admin, doctor, $"{label} Patient", ct);
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

        return (doctor, patient, patientProfileId, caseId);
    }

    /// <summary>SetupConfirmedCaseAsync + 1 thuốc Active có đủ tồn kho + kê đơn 3 khung uống
    /// (Morning/Noon/Evening) trong durationDays ngày. Trả về đủ thông tin cho các case dùng
    /// intake log / adherence tracking phía sau.</summary>
    private static async Task<(HttpClient Doctor, HttpClient Patient, Guid PatientProfileId, Guid CaseId, Guid PrescriptionId)>
        SetupPrescribedCaseAsync(
            WebApplicationFactory<Program> app, HttpClient admin, string label, CancellationToken ct, short durationDays)
    {
        var (doctor, patient, patientProfileId, caseId) = await SetupConfirmedCaseAsync(app, admin, label, ct);
        var (_, medicineName) = await CreateActiveMedicineWithStockAsync(admin, label, ct);

        var prescribeResponse = await PrescribeAsync(doctor, caseId, medicineName, durationDays, ct);
        prescribeResponse.EnsureSuccessStatusCode();
        var prescription = await prescribeResponse.Content.ReadFromJsonAsync<PrescriptionResponse>(JsonOptions, ct);

        return (doctor, patient, patientProfileId, caseId, prescription!.PrescriptionId);
    }

    private static async Task<HttpResponseMessage> PrescribeAsync(
        HttpClient doctor, Guid caseId, string medicineName, short durationDays, CancellationToken ct) =>
        await doctor.PostAsJsonAsync("/api/v1/prescriptions", new
        {
            caseId,
            generalNote = (string?)null,
            items = new[]
            {
                new
                {
                    medicineName,
                    quantityPerDose = 1,
                    durationDays,
                    startDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    instructions = (string?)null,
                    scheduleSlots = new[] { "Morning", "Noon", "Evening" },
                },
            },
        }, ct);

    /// <summary>Tạo 1 thuốc Active + 1 đơn vị đóng gói bán được + nhập kho đủ dùng cho test.
    /// medicine_unit đã được seed thủ công 1 dòng "Viên" (SeedMedicineUnitId) — bảng tra cứu
    /// trống, không có API tạo, giống symptom_categories ở BF-04.</summary>
    private static async Task<(Guid MedicineId, string MedicineName)> CreateActiveMedicineWithStockAsync(
        HttpClient admin, string label, CancellationToken ct)
    {
        var medicineName = $"{label} Med {Guid.NewGuid():N}";

        var medicineResponse = await admin.PostAsJsonAsync("/api/v1/medicines", new
        {
            name = medicineName,
            usageUnit = (string?)null,
            volumePerBaseUnit = (decimal?)null,
            medicineUnitId = SeedMedicineUnitId,
            salePrice = 5000,
            lowStockThreshold = 5,
        }, ct);
        medicineResponse.EnsureSuccessStatusCode();
        var medicine = await medicineResponse.Content.ReadFromJsonAsync<MedicineResponse>(JsonOptions, ct);

        // CreateMedicineAsync đã tự tạo sẵn 1 packaging cơ sở (IsBaseUnit=true, IsSellable=true,
        // ConversionFactor=1) dùng chính MedicineUnitId vừa gửi — AddPackagingAsync luôn từ chối
        // IsBaseUnit=true ("đã có đơn vị cơ sở") nên KHÔNG được gọi POST packagings ở đây, chỉ
        // cần đọc lại packaging cơ sở đã có.
        var packagingsResponse = await admin.GetAsync($"/api/v1/medicines/{medicine!.MedicineId}/packagings", ct);
        packagingsResponse.EnsureSuccessStatusCode();
        var packagings = await packagingsResponse.Content.ReadFromJsonAsync<List<MedicinePackagingResponse>>(JsonOptions, ct);
        var packaging = packagings!.Single(p => p.IsBaseUnit);

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new
        {
            name = $"{label} Supplier {Guid.NewGuid():N}"[..30],
            phoneNumber = UniquePhone(),
            email = $"{Guid.NewGuid():N}@test.adsus.local",
            address = "123 Test Street",
            taxCode = Random.Shared.Next(1_000_000_000, 2_000_000_000).ToString(),
        }, ct);
        supplierResponse.EnsureSuccessStatusCode();
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierResponse>(JsonOptions, ct);

        var importResponse = await admin.PostAsJsonAsync("/api/v1/inventory/import", new
        {
            medicineId = medicine.MedicineId,
            supplierId = supplier!.SupplierId,
            medicinePackagingId = packaging!.Id,
            lotNumber = UniqueCode(),
            expiryDate = DateTime.UtcNow.AddYears(1),
            quantity = 1000,
            importPricePerUnit = 1000,
        }, ct);
        importResponse.EnsureSuccessStatusCode();

        return (medicine.MedicineId, medicineName);
    }

    private static async Task<Guid> GetFirstIntakeIdAsync(HttpClient patient, Guid prescriptionId, CancellationToken ct)
    {
        var response = await patient.GetAsync($"/api/v1/me/medication-intakes/prescription/{prescriptionId}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<IntakeLogResponse>>>(JsonOptions, ct);
        return body!.Data!.OrderBy(i => i.ScheduledTime).First().IntakeId;
    }

    /// <summary>Lùi ScheduledTime của 1 intake log đã tồn tại về quá khứ/tương lai theo offset.
    /// Dùng thẳng AppDbContext của app (cùng 1 DB test thật) để backdate dữ liệu setup — không
    /// tráo hay mock MedicationIntakeService đang được kiểm thử; lệnh confirm/remind sau đó vẫn
    /// đi xuyên suốt qua service thật.</summary>
    private static async Task BackdateIntakeAsync(
        WebApplicationFactory<Program> app, Guid intakeId, TimeSpan offsetFromNow, CancellationToken ct)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.MedicationIntakeLogs.FirstAsync(l => l.IntakeId == intakeId, ct);
        log.ScheduledTime = DateTime.UtcNow.Add(offsetFromNow);
        await db.SaveChangesAsync(ct);
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

    /// <summary>Không còn endpoint tạo 1 slot thủ công (POST /api/v1/schedule-slots đã bị gỡ khỏi
    /// ScheduleSlotsController) — slot giờ chỉ sinh được qua ensure-default rồi lấy 1 ca Open.</summary>
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
