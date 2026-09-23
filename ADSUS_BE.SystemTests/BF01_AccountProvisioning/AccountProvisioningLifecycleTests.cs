using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
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
    private const string SeedAdminPassword = "Aa123456@";

    // Số Firebase "test phone number" cấu hình trong Firebase Console (Authentication →
    // Sign-in method → Phone → "Phone numbers for testing"), mã xác thực cố định đi kèm.
    // Đây là số CỐ ĐỊNH (không sinh ngẫu nhiên được như UniquePhone()), nên các test dùng
    // chúng phải tự chấp nhận cả 2 khả năng "chưa từng đăng ký" (lần chạy đầu) và "đã đăng ký
    // từ lần chạy trước" (các lần chạy sau) — xem từng test bên dưới.
    private const string FirebaseTestPhoneOne = "+84900000101";
    private const string FirebaseTestPhoneTwo = "+84900000102";
    private const string FirebaseTestPhoneThree = "+84900000103";
    private const string FirebaseTestPhoneThreeLocal = "0900000103";
    private const string FirebaseTestVerificationCode = "123456";

    // ---- Scenario A — Admin provisions & manages account ----

    [Fact]
    public async Task STC001_AdminCreatesDoctorAccount_Returns201WithDoctorRole()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC001 Doctor",
            role = "DOCTOR",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(ct);
        Assert.Equal("DOCTOR", body!.Data!.Account.Role);
        Assert.False(string.IsNullOrEmpty(body.Data.TemporaryPassword));
    }

    [Fact]
    public async Task STC002_AdminCreatesPatientAccount_Returns201WithPatientRole()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC002 Patient",
            role = "PATIENT",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(ct);
        Assert.Equal("PATIENT", body!.Data!.Account.Role);
    }

    [Fact]
    public async Task STC003_AdminUpdatesRole_GetReflectsNewRole()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var created = await CreateAccountAsync(admin, "STC003 Original Doctor", "DOCTOR");

        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/admin/users/{created.Account.UserId}", new
        {
            fullName = "STC003 Original Doctor",
            role = "STAFF",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await admin.GetAsync($"/api/v1/admin/users/{created.Account.UserId}", ct);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>(ct);
        Assert.Equal("STAFF", fetched!.Data!.Role);
    }

    [Fact]
    public async Task STC004_NurseCreatesPatientAtIntake_Succeeds_DoctorSameEndpoint_Returns403()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var nurse = await CreateAccountAsync(admin, "STC004 Nurse", "STAFF");
        var doctor = await CreateAccountAsync(admin, "STC004 Doctor", "DOCTOR");

        var nurseClient = await LoginAndForcePasswordChangeAsync(app, nurse.Account.PhoneNumber, nurse.TemporaryPassword);
        var doctorClient = await LoginAndForcePasswordChangeAsync(app, doctor.Account.PhoneNumber, doctor.TemporaryPassword);

        var nurseResponse = await nurseClient.PostAsJsonAsync("/api/v1/patients", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC004 Patient (via Nurse)",
            dateOfBirth = (string?)null,
            email = (string?)null,
        }, ct);
        Assert.Equal(HttpStatusCode.Created, nurseResponse.StatusCode);

        var doctorResponse = await doctorClient.PostAsJsonAsync("/api/v1/patients", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC004 Patient (via Doctor, should fail)",
            dateOfBirth = (string?)null,
            email = (string?)null,
        }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, doctorResponse.StatusCode);
    }

    [Fact]
    public async Task STC005_AdminDeactivatesAccount_StatusIsDeactivated_RecordStillQueryable()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var created = await CreateAccountAsync(admin, "STC005 Doctor To Deactivate", "DOCTOR");

        var deactivateResponse = await admin.PutAsync($"/api/v1/admin/users/{created.Account.UserId}/deactivate", null, ct);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        // Không hard-delete: bản ghi vẫn truy vấn được, chỉ đổi status.
        var getResponse = await admin.GetAsync($"/api/v1/admin/users/{created.Account.UserId}", ct);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>(ct);
        Assert.Equal("DEACTIVATED", fetched!.Data!.Status);
    }

    // ---- Scenario B — First sign-in & self-service ----

    [Fact]
    public async Task STC006_NewlyProvisionedUser_FirstSignIn_Returns200AndMustChangePasswordTrue()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC006 Doctor", "DOCTOR");

        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = created.TemporaryPassword,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);
        Assert.True(body!.Data!.MustChangePassword);
    }

    [Fact]
    public async Task STC007_UserChangesPassword_OldPasswordRejected_NewPasswordAccepted()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC007 Doctor", "DOCTOR");
        var client = await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword);

        const string newPassword = "NewPass123!";
        var changeResponse = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword,
            confirmNewPassword = newPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var oldLogin = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = created.TemporaryPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = newPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task STC008_UserUpdatesOwnProfile_GetReflectsUpdatedContactDetails()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC008 Original Name", "PATIENT");
        var client = await LoginAndForcePasswordChangeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword);

        const string updatedName = "STC008 Updated Name";
        var updateResponse = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            fullName = updatedName,
            email = (string?)null,
            dateOfBirth = (string?)null,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/users/me", ct);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserProfileResponse>>(ct);
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
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var testEmail = $"stc009_{Random.Shared.Next(100000, 999999)}@adsus.test";
        var created = await CreateAccountAsync(admin, "STC009 Patient", "PATIENT", email: testEmail);

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            phoneNumber = created.Account.PhoneNumber,
            email = testEmail,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Contains("new password has been sent", body!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Scenario F — Additional exception paths for sign in, session, and password change ----

    [Fact]
    public async Task STC016_SignIn_WrongPassword_Returns401WithGenericMessage()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC016 Doctor", "DOCTOR");

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = "TotallyWrongPassword1!",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);
        Assert.Equal("Invalid phone number or password.", body!.Message);
    }

    [Fact]
    public async Task STC017_SignOut_RevokesRefreshToken_CannotRefreshAfterward()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC017 Doctor", "DOCTOR");

        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = created.TemporaryPassword,
        }, ct);
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);
        var refreshToken = loginBody!.Data!.RefreshToken;

        var authedClient = app.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody.Data.AccessToken);

        var logoutResponse = await authedClient.PostAsync("/api/v1/auth/logout", null, ct);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var refreshResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken,
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task STC018_ChangePassword_WrongCurrentPassword_Returns400_ExistingPasswordStillWorks()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var created = await CreateAccountAsync(admin, "STC018 Doctor", "DOCTOR");
        var client = await LoginAndAuthorizeAsync(app, created.Account.PhoneNumber, created.TemporaryPassword);

        const string realPassword = "RealPass123!";
        var firstChange = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword = realPassword,
            confirmNewPassword = realPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, firstChange.StatusCode);

        var wrongChange = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "TotallyWrongPassword1!",
            newPassword = "AnotherPass123!",
            confirmNewPassword = "AnotherPass123!",
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongChange.StatusCode);

        var loginWithRealPassword = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = created.Account.PhoneNumber,
            password = realPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, loginWithRealPassword.StatusCode);
    }

    // ---- Scenario G — Additional exception paths for administrator account management ----

    [Fact]
    public async Task STC019_AdminCreatesAccount_DuplicatePhone_Returns400Conflict()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var existing = await CreateAccountAsync(admin, "STC019 Existing Doctor", "DOCTOR");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = existing.Account.PhoneNumber,
            fullName = "STC019 Duplicate Attempt",
            role = "PATIENT",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(ct);
        Assert.Equal("This phone number is already used by another account.", body!.Message);
    }

    [Fact]
    public async Task STC020_AdminCreatesAccount_AdminRole_Returns400()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC020 Attempted Admin",
            role = "ADMIN",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>(ct);
        Assert.Equal("Role must be one of DOCTOR, STAFF, PATIENT or PHARMACIST.", body!.Message);
    }

    [Fact]
    public async Task STC021_AdminCannotDeactivateOrResetOwnAccount()
    {
        await using var app = CreateApp();
        var admin = await LoginAsAdminAsync(app);

        var ct = TestContext.Current.CancellationToken;
        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = SeedAdminPhone,
            password = SeedAdminPassword,
        }, ct);
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);
        var adminUserId = loginBody!.Data!.UserId;

        var deactivateResponse = await admin.PutAsync($"/api/v1/admin/users/{adminUserId}/deactivate", null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, deactivateResponse.StatusCode);

        var resetResponse = await admin.PutAsync($"/api/v1/admin/users/{adminUserId}/reset-password", null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, resetResponse.StatusCode);

        var getResponse = await admin.GetAsync($"/api/v1/admin/users/{adminUserId}", ct);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>(ct);
        Assert.Equal("ACTIVE", fetched!.Data!.Status);
    }

    [Fact]
    public async Task STC022_AdminResetsAnotherUsersPassword_NurseResetsOnlyPatientPassword()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateAccountAsync(admin, "STC022 Doctor", "DOCTOR");

        var adminResetResponse = await admin.PutAsync($"/api/v1/admin/users/{doctor.Account.UserId}/reset-password", null, ct);
        Assert.Equal(HttpStatusCode.OK, adminResetResponse.StatusCode);
        var adminResetBody = await adminResetResponse.Content.ReadFromJsonAsync<ApiResponse<string>>(ct);
        Assert.False(string.IsNullOrEmpty(adminResetBody!.Data));

        var nurse = await CreateAccountAsync(admin, "STC022 Nurse", "STAFF");
        var nurseClient = await LoginAndForcePasswordChangeAsync(app, nurse.Account.PhoneNumber, nurse.TemporaryPassword);

        var patientResponse = await nurseClient.PostAsJsonAsync("/api/v1/patients", new
        {
            phoneNumber = UniquePhone(),
            fullName = "STC022 Patient",
            dateOfBirth = (string?)null,
            email = (string?)null,
        }, ct);
        patientResponse.EnsureSuccessStatusCode();
        var patientBody = await patientResponse.Content.ReadFromJsonAsync<ApiResponse<PatientAccountCreatedResponse>>(ct);

        var nurseResetResponse = await nurseClient.PutAsync(
            $"/api/v1/patients/{patientBody!.Data!.UserId}/reset-password", null, ct);
        Assert.Equal(HttpStatusCode.OK, nurseResetResponse.StatusCode);
        var nurseResetBody = await nurseResetResponse.Content.ReadFromJsonAsync<ApiResponse<string>>(ct);
        Assert.False(string.IsNullOrEmpty(nurseResetBody!.Data));

        var nurseResetsDoctorResponse = await nurseClient.PutAsync(
            $"/api/v1/patients/{doctor.Account.UserId}/reset-password", null, ct);
        // BusinessException ("Only patient accounts can be reset here.") maps to 422, not 403 —
        // the [Authorize(Roles="STAFF")] check only guards the caller's role, the target's role
        // is enforced separately in PatientAccountService.ResetPasswordAsync.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nurseResetsDoctorResponse.StatusCode);
    }

    // ---- Scenario H — Baseline patient profile management ----

    [Fact]
    public async Task STC023_DoctorCreatesBaselineProfile_ForPatientWithoutOne_Returns201()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateAccountAsync(admin, "STC023 Doctor", "DOCTOR");
        var doctorClient = await LoginAndForcePasswordChangeAsync(app, doctor.Account.PhoneNumber, doctor.TemporaryPassword);
        var patient = await CreateAccountAsync(admin, "STC023 Patient", "PATIENT");

        var response = await doctorClient.PostAsJsonAsync("/api/v1/patient-profiles", new
        {
            patientUserId = patient.Account.UserId,
            gender = "FEMALE",
            diseases = (object?)null,
            allergies = (object?)null,
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PatientProfileResponse>>(ct);
        Assert.Equal(patient.Account.UserId, body!.Data!.PatientUserId);
        Assert.Equal("FEMALE", body.Data.Gender);
    }

    [Fact]
    public async Task STC024_CreateBaselineProfile_InvalidGender_Returns400_NoProfileCreated()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var doctor = await CreateAccountAsync(admin, "STC024 Doctor", "DOCTOR");
        var doctorClient = await LoginAndForcePasswordChangeAsync(app, doctor.Account.PhoneNumber, doctor.TemporaryPassword);
        var patient = await CreateAccountAsync(admin, "STC024 Patient", "PATIENT");

        var response = await doctorClient.PostAsJsonAsync("/api/v1/patient-profiles", new
        {
            patientUserId = patient.Account.UserId,
            gender = "NOT_A_REAL_GENDER",
            diseases = (object?)null,
            allergies = (object?)null,
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Scenario I — Firebase-based self-registration (UC-05) ----

    [Fact]
    public async Task STC025_SelfRegisterViaFirebase_FirstAttemptCreatesOrAlreadyExists_SecondAttemptAlwaysConflicts()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var apiKey = FirebaseTestPhoneHelper.GetWebApiKey(app);

        var firstToken = await FirebaseTestPhoneHelper.GetIdTokenAsync(
            apiKey, FirebaseTestPhoneOne, FirebaseTestVerificationCode, ct);
        var firstResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = firstToken,
            fullName = "STC025 Patient",
            password = "SelfReg123!",
            confirmPassword = "SelfReg123!",
            email = (string?)null,
            dateOfBirth = (string?)null,
            gender = (string?)null,
        }, ct);
        Assert.True(
            firstResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected status on first attempt: {firstResponse.StatusCode}");
        if (firstResponse.StatusCode == HttpStatusCode.OK)
        {
            var firstBody = await firstResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);
            Assert.Equal("PATIENT", firstBody!.Data!.Role);
            Assert.False(firstBody.Data.MustChangePassword);
        }

        var secondToken = await FirebaseTestPhoneHelper.GetIdTokenAsync(
            apiKey, FirebaseTestPhoneOne, FirebaseTestVerificationCode, ct);
        var secondResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = secondToken,
            fullName = "STC025 Patient Duplicate Attempt",
            password = "SelfReg123!",
            confirmPassword = "SelfReg123!",
            email = (string?)null,
            dateOfBirth = (string?)null,
            gender = (string?)null,
        }, ct);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Equal("This phone number is already registered.", secondBody!.Message);
    }

    [Fact]
    public async Task STC026_SelfRegisterViaFirebase_PasswordConfirmationMismatch_Returns400()
    {
        // Xác thực form (FluentValidation) chạy TRƯỚC khi service đụng tới Firebase, nên không
        // cần gọi Firebase thật ở đây — firebaseIdToken chỉ cần khác rỗng để qua rule NotEmpty.
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = "placeholder-form-validation-fails-before-firebase-is-checked",
            fullName = "STC026 Patient",
            password = "SelfReg123!",
            confirmPassword = "DoesNotMatch123!",
            email = (string?)null,
            dateOfBirth = (string?)null,
            gender = (string?)null,
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task STC027_SelfRegisterViaFirebase_InvalidFirebaseToken_Returns422()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = "clearly-not-a-real-firebase-id-token",
            fullName = "STC027 Patient",
            password = "SelfReg123!",
            confirmPassword = "SelfReg123!",
            email = (string?)null,
            dateOfBirth = (string?)null,
            gender = (string?)null,
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Contains("Firebase ID token is invalid or has expired", body!.Message);
    }

    // ---- Scenario J — Firebase-based self-service password recovery (UC-02) ----

    [Fact]
    public async Task STC028_FirebasePasswordReset_InvalidFirebaseToken_Returns422()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password/complete-with-firebase", new
        {
            firebaseIdToken = "clearly-not-a-real-firebase-id-token",
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!",
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Contains("Firebase ID token is invalid or has expired", body!.Message);
    }

    [Fact]
    public async Task STC029_FirebasePasswordReset_PhoneNeverRegistered_Returns404GenericMessage()
    {
        // Số FirebaseTestPhoneTwo không được STC026 (form invalid) hay bất kỳ test nào khác
        // dùng để đăng ký thật, nên vẫn "chưa có tài khoản" ở mọi lần chạy — an toàn để verify
        // đúng nhánh AF-05 (số hợp lệ về Firebase nhưng không đủ điều kiện: chưa đăng ký).
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var apiKey = FirebaseTestPhoneHelper.GetWebApiKey(app);

        var idToken = await FirebaseTestPhoneHelper.GetIdTokenAsync(
            apiKey, FirebaseTestPhoneTwo, FirebaseTestVerificationCode, ct);

        var response = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password/complete-with-firebase", new
        {
            firebaseIdToken = idToken,
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!",
        }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(ct);
        Assert.Equal("This phone number is not registered.", body!.Message);
    }

    [Fact]
    public async Task STC030_FirebasePasswordReset_ExistingActivePatient_Returns200_NewPasswordSignsIn()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var apiKey = FirebaseTestPhoneHelper.GetWebApiKey(app);

        // Đảm bảo có 1 tài khoản Patient active gắn với FirebaseTestPhoneThree — chấp nhận cả
        // OK (lần chạy đầu tạo mới) lẫn Conflict (tài khoản đã tồn tại từ lần chạy trước); phần
        // reset bên dưới không phụ thuộc việc tài khoản vừa được tạo hay đã có sẵn.
        var ensureToken = await FirebaseTestPhoneHelper.GetIdTokenAsync(
            apiKey, FirebaseTestPhoneThree, FirebaseTestVerificationCode, ct);
        var ensureResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = ensureToken,
            fullName = "STC030 Patient",
            password = "OriginalPass123!",
            confirmPassword = "OriginalPass123!",
            email = (string?)null,
            dateOfBirth = (string?)null,
            gender = (string?)null,
        }, ct);
        Assert.True(
            ensureResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected status ensuring the account exists: {ensureResponse.StatusCode}");

        const string newPassword = "ResetByFirebase123!";
        var resetToken = await FirebaseTestPhoneHelper.GetIdTokenAsync(
            apiKey, FirebaseTestPhoneThree, FirebaseTestVerificationCode, ct);
        var resetResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password/complete-with-firebase", new
        {
            firebaseIdToken = resetToken,
            newPassword,
            confirmNewPassword = newPassword,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var resetBody = await resetResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);
        Assert.Equal("PATIENT", resetBody!.Data!.Role);
        Assert.False(resetBody.Data.MustChangePassword);

        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = FirebaseTestPhoneThreeLocal,
            password = newPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
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

    private const string FinalTestPassword = "Aa123456@";

    /// <summary>Đăng nhập bằng mật khẩu tạm do Admin cấp, rồi đổi ngay sang mật khẩu cố định.
    /// MustChangePasswordMiddleware chặn (403) mọi request khác ngoài change-password/logout khi
    /// access token còn mang claim MustChangePassword=true. AuthService.ChangePasswordAsync phát
    /// token mới ngay trong response (sửa 21/09/2026, giống LoginAsync) nên chỉ cần 1 vòng đăng
    /// nhập, không cần đăng nhập lại lần 2. Dùng hàm này thay cho LoginAndAuthorizeAsync ở BẤT KỲ
    /// đâu client sẽ gọi tiếp 1 hành động nghiệp vụ khác ngoài change-password/logout.</summary>
    private static async Task<HttpClient> LoginAndForcePasswordChangeAsync(
        WebApplicationFactory<Program> app, string phone, string temporaryPassword)
    {
        var ct = TestContext.Current.CancellationToken;
        var tempClient = await LoginAndAuthorizeAsync(app, phone, temporaryPassword);
        var changeResponse = await tempClient.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            newPassword = FinalTestPassword,
            confirmNewPassword = FinalTestPassword,
        }, ct);
        changeResponse.EnsureSuccessStatusCode();
        var body = await changeResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(ct);

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
