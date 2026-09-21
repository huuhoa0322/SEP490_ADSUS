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
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF03_AppointmentBooking;

/// <summary>
/// Report 5.3 — BF-03 Appointment Booking & Clinic Reception (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi, mỗi case kiểm 1 quy tắc nghiệp vụ khác nhau — không lặp lại
/// cùng 1 code path dưới hình thức khác. Không tự động hoá các background job (JOB-02 sinh slot
/// mặc định, JOB-03 nhắc lịch, JOB-08 đánh NO_SHOW) vì phụ thuộc thời gian thực/Quartz scheduler,
/// không phù hợp với System Test dạng HTTP Flow đồng bộ.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01) trước khi chạy.
/// </summary>
public class AppointmentBookingTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    /// <summary>
    /// Server serialize enum (SlotStatus, AppointmentStatus, ShiftRequestType/Status...) thành
    /// chuỗi qua JsonStringEnumConverter đăng ký trong Program.cs. ReadFromJsonAsync mặc định
    /// KHÔNG có converter đó (chỉ hiểu enum dạng số) — phải tự truyền options này ở mọi chỗ
    /// deserialize DTO có field enum, nếu không sẽ ném JsonException.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_DoctorCreatesScheduleSlot_Returns201Open()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC001 Doctor", ct);

        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var response = await doctor.PostAsJsonAsync("/api/v1/schedule-slots", new
        {
            visitDate,
            startTime = "09:00:00",
            endTime = "10:00:00",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScheduleSlotResponse>>(JsonOptions, ct);
        Assert.Equal(SlotStatus.Open, body!.Data!.Status);
    }

    [Fact]
    public async Task STC002_PatientBooksOpenSlot_Returns201Booked()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC002 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC002 Patient", ct);

        var response = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC002 checkup",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        Assert.Equal(AppointmentStatus.Booked, body!.Data!.Status);
    }

    [Fact]
    public async Task STC003_BookingAnAlreadyBookedSlot_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC003 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);

        var (firstPatient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC003 First Patient", ct);
        var firstBooking = await firstPatient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC003 first booking",
        }, ct);
        firstBooking.EnsureSuccessStatusCode();

        var (secondPatient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC003 Second Patient", ct);
        var secondBooking = await secondPatient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC003 second booking, should fail",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, secondBooking.StatusCode);
    }

    [Fact]
    public async Task STC004_StaffBooksAppointmentForPatient_Returns201Booked()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC004 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (_, patientProfileId) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC004 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC004 Staff", ct);

        var response = await staff.PostAsJsonAsync("/api/v1/appointments/book-for-patient", new
        {
            patientProfileId,
            scheduleSlotId = slotId,
            reason = "STC004 booked at the clinic counter",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        Assert.Equal(AppointmentStatus.Booked, body!.Data!.Status);
    }

    [Fact]
    public async Task STC005_PatientCancelsOwnAppointment_ReasonRequired_SlotReopens()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC005 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC005 Patient", ct);

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC005 checkup",
        }, ct);
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var appointmentId = booked!.Data!.AppointmentId;

        var cancelResponse = await patient.PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/cancel", new
        {
            cancellationReason = "STC005 can no longer make it",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        Assert.Equal(AppointmentStatus.Cancelled, cancelled!.Data!.Status);

        var slot = await GetSlotAsync(doctor, slotId, ct);
        Assert.Equal(SlotStatus.Open, slot.Status);
    }

    [Fact]
    public async Task STC006_StaffReschedulesBookedAppointment_OriginalSlotReopens_NewSlotBooked()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC006 Doctor", ct);
        var originalSlotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var newSlotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC006 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC006 Staff", ct);

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = originalSlotId,
            reason = "STC006 checkup",
        }, ct);
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var appointmentId = booked!.Data!.AppointmentId;

        var rescheduleResponse = await staff.PostAsJsonAsync($"/api/v1/appointments/{appointmentId}/reschedule", new
        {
            newScheduleSlotId = newSlotId,
            rescheduleReason = "STC006 doctor unavailable at the original time",
            autoCheckin = false,
        }, ct);

        Assert.Equal(HttpStatusCode.Created, rescheduleResponse.StatusCode);
        var rescheduled = await rescheduleResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        Assert.Equal(AppointmentStatus.Booked, rescheduled!.Data!.Status);

        var originalSlot = await GetSlotAsync(doctor, originalSlotId, ct);
        Assert.Equal(SlotStatus.Open, originalSlot.Status);
        var newSlot = await GetSlotAsync(doctor, newSlotId, ct);
        Assert.Equal(SlotStatus.Booked, newSlot.Status);
    }

    [Fact]
    public async Task STC007_StaffChecksInArrivingPatient_AppointmentBecomesCompleted()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC007 Doctor", ct);
        var slotId = await CreateSlotAsync(doctor, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), ct);
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC007 Patient", ct);
        var staff = await CreateStaffAsync(app, admin, "STC007 Staff", ct);

        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC007 checkup",
        }, ct);
        var booked = await bookResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        var appointmentId = booked!.Data!.AppointmentId;

        var checkinResponse = await staff.PostAsync(
            $"/api/v1/cases/appointment/{appointmentId}/checkin", null, ct);

        Assert.Equal(HttpStatusCode.OK, checkinResponse.StatusCode);
        var checkedIn = await checkinResponse.Content.ReadFromJsonAsync<ApiResponse<AppointmentResponse>>(JsonOptions, ct);
        Assert.Equal(AppointmentStatus.Completed, checkedIn!.Data!.Status);
    }

    [Fact]
    public async Task STC008_DoctorSubmitsLeaveRequest_Returns201Pending()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC008 Doctor", ct);

        var requestDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);
        var response = await doctor.PostAsJsonAsync("/api/v1/shift-requests", new
        {
            requestType = "Leave",
            requestDate,
            shiftType = "Morning",
            reason = "STC008 personal leave",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftRequestResponse>>(JsonOptions, ct);
        Assert.Equal(ShiftRequestStatus.Pending, body!.Data!.Status);
    }

    [Fact]
    public async Task STC009_AdminApprovesLeaveRequest_LinkedBookedAppointmentIsCancelled_SlotCloses()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateDoctorAsync(app, admin, "STC009 Doctor", ct);

        var leaveDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);
        var slotId = await CreateSlotAsync(doctor, leaveDate, ct, startTime: "09:00:00", endTime: "10:00:00");
        var (patient, _) = await CreatePatientWithProfileAsync(app, admin, doctor, "STC009 Patient", ct);
        var bookResponse = await patient.PostAsJsonAsync("/api/v1/appointments", new
        {
            scheduleSlotId = slotId,
            reason = "STC009 checkup, will be displaced by leave",
        }, ct);
        bookResponse.EnsureSuccessStatusCode();

        var leaveResponse = await doctor.PostAsJsonAsync("/api/v1/shift-requests", new
        {
            requestType = "Leave",
            requestDate = leaveDate,
            shiftType = "Morning",
            reason = "STC009 personal leave, displaces the morning booking",
        }, ct);
        leaveResponse.EnsureSuccessStatusCode();
        var leave = await leaveResponse.Content.ReadFromJsonAsync<ApiResponse<ShiftRequestResponse>>(JsonOptions, ct);
        var requestId = leave!.Data!.RequestId;

        var approveResponse = await admin.PutAsJsonAsync($"/api/v1/admin/shift-requests/{requestId}/review", new
        {
            decision = "APPROVED",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var slot = await GetSlotAsync(doctor, slotId, ct);
        Assert.Equal(SlotStatus.Closed, slot.Status);
        Assert.Empty(slot.BookedAppointments);
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

    /// <summary>Tạo tài khoản Patient (Admin) rồi lập hồ sơ y tế nền (Doctor) — điều kiện bắt
    /// buộc trước khi Patient tự đặt lịch được (AppointmentsController.GetPatientProfileIdAsync
    /// ném lỗi nếu chưa có PatientProfile). Trả về cả PatientProfileId vì
    /// UserProfileResponse (GET /users/me) không có trường này — đường duy nhất lấy được ID là
    /// từ chính response lúc tạo hồ sơ.</summary>
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

    private static async Task<ScheduleSlotResponse> GetSlotAsync(HttpClient doctor, Guid slotId, CancellationToken ct)
    {
        var response = await doctor.GetAsync($"/api/v1/schedule-slots/{slotId}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScheduleSlotResponse>>(JsonOptions, ct);
        return body!.Data!;
    }
}
