using System.Text.RegularExpressions;
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
public partial class FirebasePhoneVerificationService : IFirebasePhoneVerificationService
{
    private const string VietnamCountryCodePrefix = "+84";
    private const string PhoneNumberClaim = "phone_number";
    private const string AuthTimeClaim = "auth_time";

    // Bằng đúng ResetTokenValidity của cơ chế OTP tự quản lý trước đây — token Firebase sống 1
    // giờ và tái tạo được vô hạn lần từ refresh token, nên PHẢI tự giới hạn "độ mới" của lần xác
    // thực số điện thoại thay vì tin cậy hạn dùng mặc định của token (xem review cuối plan).
    private static readonly TimeSpan MaxPhoneVerificationAge = TimeSpan.FromMinutes(10);

    [GeneratedRegex(@"^\+84\d{9}$")]
    private static partial Regex VietnamE164Regex();

    private static readonly object FirebaseAppInitLock = new();

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

            if (!TryGetAuthTimeUtc(decoded.Claims, out var authTimeUtc)
                || DateTimeOffset.UtcNow - authTimeUtc > MaxPhoneVerificationAge)
            {
                // Token Firebase còn hiệu lực chữ ký (1 giờ, tái tạo được vô hạn lần từ refresh
                // token) KHÔNG đồng nghĩa lần xác thực số điện thoại còn "mới" — auth_time mới
                // là thời điểm người dùng thực sự nhập mã SMS. Không giới hạn ở đây thì việc
                // chứng minh sở hữu số điện thoại sẽ không bao giờ hết hạn, khác hẳn
                // VerificationTokenExpiresAt (10 phút) của cơ chế OTP cũ.
                _logger.LogWarning("Firebase ID Token đã xác thực số điện thoại quá lâu trước đó — cần xác thực lại.");
                return null;
            }

            var localPhone = ToLocalPhoneNumber(e164Phone);
            if (localPhone is null)
            {
                // Firebase Phone Auth không tự giới hạn theo quốc gia trừ khi cấu hình riêng ở
                // Console — không được tin số không phải Việt Nam đi tiếp vào users.phone (vốn
                // đã bỏ hết validator định dạng số khi chuyển sang Firebase, xem Global
                // Constraints), kẻo vi phạm PhoneNumberRule ở mọi nơi khác trong hệ thống.
                _logger.LogWarning("Firebase ID Token có phone_number không đúng định dạng số Việt Nam.");
                return null;
            }

            return localPhone;
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

    /// <summary>
    /// Biên giới chuyển đổi E.164 → nội địa DUY NHẤT phía backend (xem Global Constraints).
    /// Trả về null cho bất kỳ số nào không đúng dạng "+84" + 9 chữ số — hàm TOÀN VẸN (total),
    /// không có đường nào để một số không phải Việt Nam lọt qua dưới dạng chuỗi gốc.
    /// </summary>
    private static string? ToLocalPhoneNumber(string e164Phone) =>
        VietnamE164Regex().IsMatch(e164Phone)
            ? "0" + e164Phone[VietnamCountryCodePrefix.Length..]
            : null;

    private static bool TryGetAuthTimeUtc(
        IReadOnlyDictionary<string, object> claims, out DateTimeOffset authTimeUtc)
    {
        authTimeUtc = default;
        if (!claims.TryGetValue(AuthTimeClaim, out var raw)) return false;

        try
        {
            authTimeUtc = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(raw));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            return false;
        }
    }

    /// <summary>
    /// Mirror đúng thứ tự ưu tiên nguồn credential của <c>FirebasePushNotificationClient</c>
    /// (FIREBASE_CREDENTIALS_JSON &gt; FIREBASE_CREDENTIALS_PATH &gt; Firebase:ServiceAccountPath),
    /// KHÔNG tái dùng trực tiếp method private của lớp đó — trùng lặp nhỏ có chủ đích, cùng
    /// tinh thần đã áp dụng cho PatientSelfRegistrationService/PasswordResetOtpService ở plan
    /// OTP trước đó, để 2 lớp độc lập hoàn toàn với nhau.
    /// </summary>
    private static void EnsureFirebaseAppInitialized(IConfiguration configuration)
    {
        if (FirebaseApp.DefaultInstance != null) return;

        // Double-checked locking — init chuyển từ constructor sang đây (request path) để DI vẫn
        // resolve được trong test host thiếu credentials, nên 2 request đầu tiên có thể cùng lúc
        // gọi hàm này; không khoá thì cả 2 cùng qua được check ở trên và cùng gọi FirebaseApp.Create,
        // bên thua throw ArgumentException không khớp catch nào trong VerifyAndGetLocalPhoneNumberAsync.
        lock (FirebaseAppInitLock)
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
}
