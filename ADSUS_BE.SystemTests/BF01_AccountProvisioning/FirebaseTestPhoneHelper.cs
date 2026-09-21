using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ADSUS_BE.SystemTests.BF01_AccountProvisioning;

/// <summary>
/// Lấy 1 Firebase ID Token thật bằng REST API (Identity Toolkit), dùng số điện thoại test đã
/// cấu hình trong Firebase Console (Authentication → Sign-in method → Phone → "Phone numbers
/// for testing"). Firebase bỏ qua reCAPTCHA cho đúng các số này, nên gọi REST trực tiếp không
/// cần trình duyệt — token trả về THẬT, backend xác minh qua Firebase Admin SDK y hệt token từ
/// điện thoại thật (xem FirebasePhoneVerificationService.cs).
///
/// Key đọc từ user-secrets của chính ADSUS_BE ("Firebase:WebApiKey") — CÙNG chỗ với
/// ConnectionStrings/JwtSettings, qua IConfiguration của app trong WebApplicationFactory.
/// KHÔNG BAO GIỜ hard-code giá trị thật vào đây hay bất kỳ file nào trong repo.
/// </summary>
public static class FirebaseTestPhoneHelper
{
    private const string IdentityToolkitBaseUrl = "https://identitytoolkit.googleapis.com/v1/accounts";

    /// <summary>
    /// Đọc "Firebase:WebApiKey" từ IConfiguration của app đang chạy trong WebApplicationFactory
    /// (tức đúng user-secrets của ADSUS_BE) — báo lỗi rõ ràng ngay nếu thiếu, thay vì để 401/400
    /// mơ hồ ở bước gọi Firebase.
    /// </summary>
    public static string GetWebApiKey(WebApplicationFactory<Program> app)
    {
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        var apiKey = configuration["Firebase:WebApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Chua co 'Firebase:WebApiKey' trong user-secrets cua ADSUS_BE. Chay: " +
                "dotnet user-secrets set \"Firebase:WebApiKey\" \"<gia tri that>\" --project ADSUS_BE");
        }

        return apiKey;
    }

    /// <summary>
    /// Đăng nhập bằng 1 số điện thoại test + mã xác thực cố định, trả về Firebase ID Token thật.
    /// </summary>
    /// <param name="apiKey">Lấy qua <see cref="GetWebApiKey"/>.</param>
    /// <param name="phoneNumberE164">Định dạng "+84XXXXXXXXX", đúng số đã khai trong Console.</param>
    /// <param name="verificationCode">Mã cố định đã gán cho số đó trong Console (vd "123456").</param>
    public static async Task<string> GetIdTokenAsync(
        string apiKey,
        string phoneNumberE164,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();

        var sendCodeResponse = await http.PostAsJsonAsync(
            $"{IdentityToolkitBaseUrl}:sendVerificationCode?key={apiKey}",
            new SendVerificationCodeRequest(phoneNumberE164, "ignored-for-test-numbers"),
            cancellationToken);
        sendCodeResponse.EnsureSuccessStatusCode();
        var sendCodeBody = await sendCodeResponse.Content
            .ReadFromJsonAsync<SendVerificationCodeResponse>(cancellationToken);

        var signInResponse = await http.PostAsJsonAsync(
            $"{IdentityToolkitBaseUrl}:signInWithPhoneNumber?key={apiKey}",
            new SignInWithPhoneNumberRequest(sendCodeBody!.SessionInfo, verificationCode),
            cancellationToken);
        signInResponse.EnsureSuccessStatusCode();
        var signInBody = await signInResponse.Content
            .ReadFromJsonAsync<SignInWithPhoneNumberResponse>(cancellationToken);

        return signInBody!.IdToken;
    }

    private sealed record SendVerificationCodeRequest(
        [property: JsonPropertyName("phoneNumber")] string PhoneNumber,
        [property: JsonPropertyName("recaptchaToken")] string RecaptchaToken);

    private sealed record SendVerificationCodeResponse(
        [property: JsonPropertyName("sessionInfo")] string SessionInfo);

    private sealed record SignInWithPhoneNumberRequest(
        [property: JsonPropertyName("sessionInfo")] string SessionInfo,
        [property: JsonPropertyName("code")] string Code);

    private sealed record SignInWithPhoneNumberResponse(
        [property: JsonPropertyName("idToken")] string IdToken);
}
