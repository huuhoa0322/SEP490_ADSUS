using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF01_AccountProvisioning;

/// <summary>
/// Report 5.3 — BF-01 Account Provisioning &amp; Lifecycle, Scenario A + B (HTTP Flow).
///
/// KHÁC ADSUS_BE.IntegrationTests: KHÔNG tráo bất kỳ repository nào bằng Mock/Fake — mọi
/// request đi xuyên suốt qua tầng DAL thật, chạm vào DB test thật (đúng tinh thần "không mock
/// gì" của System Test, testing-convention §1). Yêu cầu: appsettings/user-secrets của
/// ADSUS_BE đang trỏ vào 1 Supabase project TEST riêng, không phải production — WebApplicationFactory
/// dùng chung UserSecretsId với ADSUS_BE.Program, không cần cấu hình riêng.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL trước khi chạy (API không cho tạo Admin
/// — validator chặn cứng role ADMIN, xem CreateUserAccountRequestValidator). Đổi 2 hằng số
/// SeedAdminPhone/SeedAdminPassword bên dưới nếu bạn seed khác giá trị mặc định.
///
/// Số điện thoại tài khoản Doctor/Nurse/Patient tạo mới trong mỗi test được sinh NGẪU NHIÊN
/// (UniquePhone()) để chạy lại nhiều lần không bị lỗi trùng — dữ liệu tích luỹ dần trong DB
/// test, không tự dọn dẹp (quyết định đã chốt khi viết bộ test này).
/// </summary>
public class AccountProvisioningLifecycleTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Test123456@";

    // ---- Scenario A — Admin provisions & manages account ----

    [Fact]
    public async Task STC001_AdminCreatesDoctorAccount_Returns201WithDoctorRole()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC001 Doctor",
            role = "DOCTOR",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>();
        Assert.Equal("DOCTOR", body!.Data!.Account.Role);
        Assert.False(string.IsNullOrEmpty(body.Data.TemporaryPassword));
    }

    [Fact]
    public async Task STC002_AdminCreatesPatientAccount_Returns201WithPatientRole()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC002 Patient",
            role = "PATIENT",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>();
        Assert.Equal("PATIENT", body!.Data!.Account.Role);
    }

    [Fact]
    public async Task STC003_AdminUpdatesRole_GetReflectsNewRole()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);

        var created = await CreateAccountAsync(admin, "STC003 Original Doctor", "DOCTOR");

        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/admin/users/{created.Account.UserId}", new
        {
            fullName = "STC003 Original Doctor",
            role = "NURSE",
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await admin.GetAsync($"/api/v1/admin/users/{created.Account.UserId}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>();
        Assert.Equal("NURSE", fetched!.Data!.Role);
    }

    [Fact]
    public async Task STC004_NurseCreatesPatientAtIntake_Succeeds_DoctorSameEndpoint_Returns403()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);

        var nurse = await CreateAccountAsync(admin, "STC004 Nurse", "NURSE");
        var doctor = await CreateAccountAsync(admin, "STC004 Doctor", "DOCTOR");

        var nurseClient = await LoginAndAuthorizeAsync(app, nurse.Account.PhoneNumber, nurse.TemporaryPassword);
        var doctorClient = await LoginAndAuthorizeAsync(app, doctor.Account.PhoneNumber, doctor.TemporaryPassword);

        var nurseResponse = await nurseClient.PostAsJsonAsync("/api/v1/patients", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC004 Patient (via Nurse)",
            dateOfBirth = (string?)null,
            email = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, nurseResponse.StatusCode);

        var doctorResponse = await doctorClient.PostAsJsonAsync("/api/v1/patients", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC004 Patient (via Doctor, should fail)",
            dateOfBirth = (string?)null,
            email = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, doctorResponse.StatusCode);
    }

    [Fact]
    public async Task STC005_AdminDeactivatesAccount_StatusIsDeactivated_RecordStillQueryable()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);

        var created = await CreateAccountAsync(admin, "STC005 Doctor To Deactivate", "DOCTOR");

        var deactivateResponse = await admin.PutAsync($"/api/v1/admin/users/{created.Account.UserId}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Không hard-delete: bản ghi vẫn truy vấn được, chỉ đổi status.
        var getResponse = await admin.GetAsync($"/api/v1/admin/users/{created.Account.UserId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>();
        Assert.Equal("DEACTIVATED", fetched!.Data!.Status);
    }

    // ---- Scenario B — First sign-in & self-service ----

    [Fact]
    public async Task STC006_NewlyProvisionedUser_FirstSignIn_Returns200AndMustChangePasswordTrue()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC006 Doctor", "DOCTOR");

        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = created.TemporaryPassword,
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.True(body!.Data!.MustChangePassword);
    }

    [Fact]
    public async Task STC007_UserChangesPassword_OldPasswordRejected_NewPasswordAccepted()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC007 Doctor", "DOCTOR");
        var client = await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword);

        const string newPassword = "NewPass123!";
        var changeResponse = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword,
            confirmNewPassword = newPassword,
        });
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var oldLogin = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = created.TemporaryPassword,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = newPassword,
        });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task STC008_UserUpdatesOwnProfile_GetReflectsUpdatedContactDetails()
    {
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC008 Original Name", "PATIENT");
        var client = await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword);

        const string updatedName = "STC008 Updated Name";
        var updateResponse = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            fullName = updatedName,
            email = (string?)null,
            dateOfBirth = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>();
        Assert.Equal(updatedName, body!.Data!.FullName);
    }

    [Fact]
    public async Task STC009_ForgotPassword_ValidPhoneAndEmail_Returns200GenericMessage()
    {
        // Endpoint luôn trả về ĐÚNG 1 câu chung (AuthController.ForgotPassword, GB-06-style),
        // không tiết lộ tài khoản có tồn tại hay không, và không trả token/link qua HTTP — mật
        // khẩu mới được gửi qua email thật. Vì môi trường test không bắt được email, TC này chỉ
        // xác nhận đúng hành vi HTTP quan sát được (200 + thông điệp chung), không xác nhận tiếp
        // được bước "đăng nhập bằng mật khẩu mới" bằng HTTP thuần.
        using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC009 Patient", "PATIENT", email: "stc009@adsus.test");

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            phoneNumber = created.Account.PhoneNumber,
            email = "stc009@adsus.test",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Contains("new password has been sent", body!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- helpers ----

    /// <summary>App thật, không tráo bất kỳ service nào — request chạm DB test thật.</summary>
    private static WebApplicationFactory<Program> CreateApp() => new();

    private static string UniquePhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    private static async Task<HttpClient> LoginAsAdminAsync(WebApplicationFactory<Program> app) =>
        await LoginAndAuthorizeAsync(app, SeedAdminPhone, SeedAdminPassword);

    private static async Task<HttpClient> LoginAndAuthorizeAsync(
        WebApplicationFactory<Program> app, string phone, string password)
    {
        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = phone,
            password,
        });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    private static async Task<CreatedUserAccountResponse> CreateAccountAsync(
        HttpClient admin, string fullName, string role, string? email = null)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName,
            role,
            email,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>();
        return body!.Data!;
    }
}
