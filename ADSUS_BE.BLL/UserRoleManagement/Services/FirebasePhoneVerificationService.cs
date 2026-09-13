using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Xác minh Firebase ID Token qua Firebase Admin SDK — TÁI SỬ DỤNG đúng project Firebase và
/// đúng 3 nguồn credential đã dùng cho <c>FirebasePushNotificationClient</c> (push notification),
/// KHÔNG tạo project Firebase mới, KHÔNG thêm secret mới (xem Global Constraints).
///
/// `FirebaseApp.DefaultInstance` chỉ được khởi tạo MỘT LẦN cho toàn tiến trình — class này tự
/// kiểm tra trước khi tạo (giống hệt cách `FirebasePushNotificationClient` đã làm), để 2 lớp
/// độc lập không đụng nhau dù DI resolve theo thứ tự nào, và để hoạt động cả khi
/// IPushNotificationClient đang dùng FakePushNotificationClient (Development, không khởi tạo
/// FirebaseApp) — verify phone vẫn cần Firebase thật dù push notification không cần.
/// </summary>
public class FirebasePhoneVerificationService : IFirebasePhoneVerificationService
{
    private const string VietnamCountryCodePrefix = "+84";
    private const string PhoneNumberClaim = "phone_number";

    private readonly IConfiguration _configuration;
    private readonly ILogger<FirebasePhoneVerificationService> _logger;

    public FirebasePhoneVerificationService(
        IConfiguration configuration, ILogger<FirebasePhoneVerificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> VerifyAndGetLocalPhoneNumberAsync(
        string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            EnsureFirebaseAppInitialized(_configuration);

            var decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);

            if (!decoded.Claims.TryGetValue(PhoneNumberClaim, out var phoneClaim)
                || phoneClaim is not string e164Phone
                || string.IsNullOrWhiteSpace(e164Phone))
            {
                _logger.LogWarning("Firebase ID Token hợp lệ nhưng không có claim phone_number.");
                return null;
            }

            return ToLocalPhoneNumber(e164Phone);
        }
        catch (FirebaseAuthException ex)
        {
            // Sai chữ ký, hết hạn, sai project, token bị thu hồi... — tất cả đều "không hợp lệ",
            // không phân biệt lý do ra ngoài (tầng gọi chỉ cần biết verify thất bại).
            _logger.LogWarning(ex, "Firebase ID Token không hợp lệ khi xác minh số điện thoại.");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            // Thiếu credentials (FIREBASE_CREDENTIALS_JSON/PATH/ServiceAccountPath) — lỗi CẤU HÌNH
            // server, không phải lỗi của caller. Vẫn phải trả null để giữ đúng hợp đồng "không bao
            // giờ throw ra ngoài" của interface này, nhưng log ở mức Error (khác Warning ở trên) để
            // phân biệt được với "token không hợp lệ" khi vận hành/giám sát.
            _logger.LogError(ex, "Firebase chưa được cấu hình đúng — không thể xác minh ID Token.");
            return null;
        }
    }

    /// <summary>Biên giới chuyển đổi E.164 → nội địa DUY NHẤT phía backend (xem Global Constraints).</summary>
    private static string ToLocalPhoneNumber(string e164Phone) =>
        e164Phone.StartsWith(VietnamCountryCodePrefix, StringComparison.Ordinal)
            ? "0" + e164Phone[VietnamCountryCodePrefix.Length..]
            : e164Phone;

    /// <summary>
    /// Mirror đúng thứ tự ưu tiên nguồn credential của <c>FirebasePushNotificationClient</c>
    /// (FIREBASE_CREDENTIALS_JSON &gt; FIREBASE_CREDENTIALS_PATH &gt; Firebase:ServiceAccountPath),
    /// KHÔNG tái dùng trực tiếp method private của lớp đó — trùng lặp nhỏ có chủ đích, cùng
    /// tinh thần đã áp dụng cho PatientSelfRegistrationService/PasswordResetOtpService ở plan
    /// OTP trước đó, để 2 lớp độc lập hoàn toàn với nhau.
    /// </summary>
    private void EnsureFirebaseAppInitialized(IConfiguration configuration)
    {
        if (FirebaseApp.DefaultInstance != null) return;

        var credentialsJson = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");
        if (!string.IsNullOrEmpty(credentialsJson))
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(credentialsJson));
            var credential = ServiceAccountCredential.FromServiceAccountData(stream).ToGoogleCredential();
            FirebaseApp.Create(new AppOptions { Credential = credential });
            return;
        }

        var path = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH")
            ?? configuration["Firebase:ServiceAccountPath"]
            ?? throw new InvalidOperationException(
                "Chưa cấu hình Firebase credentials (FIREBASE_CREDENTIALS_JSON / " +
                "FIREBASE_CREDENTIALS_PATH / Firebase:ServiceAccountPath) — cần để xác minh " +
                "Firebase ID Token. Đây là cùng credential đã dùng cho push notification.");

#pragma warning disable CS0618 // GoogleCredential.FromFile deprecated nhưng vẫn hoạt động
        FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) });
#pragma warning restore CS0618
    }
}
