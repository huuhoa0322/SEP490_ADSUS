using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản hiện thực <see cref="IOtpSmsService"/> gửi qua Speedsms.vn REST API — cùng phong cách
/// <see cref="SendGridEmailService"/>: không dùng SDK, chỉ 1 lời gọi HTTP. Thay eSMS.vn
/// (13/09/2026) vì eSMS Brandname bắt buộc Giấy phép Đăng ký Kinh doanh còn Speedsms Verify
/// (sms_type=4) thì không.
///
/// sms_type=4 = brandname mặc định của Speedsms ("Verify"/"Notify") — không cần tham số
/// sender (chỉ bắt buộc khi sms_type=3 hoặc 5, brandname tự đăng ký với nhà mạng).
///
/// Xác thực bằng HTTP Basic Auth: access token làm username, mật khẩu để trống — đúng cách
/// tài liệu Speedsms mô tả qua ví dụ curl (-u "{Access token}").
/// </summary>
public class SpeedsmsSmsService : IOtpSmsService
{
    private const string ApiUrl = "https://api.speedsms.vn/index.php/sms/send";
    private const string SuccessStatus = "success";

    /// <summary>sms_type "4" = brandname mặc định của Speedsms (Verify/Notify) — không cần đăng ký riêng.</summary>
    private const string DefaultBrandnameSmsType = "4";

    private readonly HttpClient _http;
    private readonly SpeedsmsSettings _settings;
    private readonly ILogger<SpeedsmsSmsService> _logger;

    public SpeedsmsSmsService(
        IHttpClientFactory httpClientFactory,
        IOptions<SpeedsmsSettings> settings,
        ILogger<SpeedsmsSmsService> logger)
    {
        _http = httpClientFactory.CreateClient("Speedsms");
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(
        string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = JsonContent.Create(new SpeedsmsRequest
            {
                To = phoneNumber,
                Content = BuildMessage(otpCode),
                SmsType = DefaultBrandnameSmsType,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.AccessToken}:")));

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Speedsms trả lỗi HTTP khi gửi OTP: {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            SpeedsmsResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<SpeedsmsResponse>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                // Speedsms trả 200 nhưng body không phải JSON hợp lệ — vẫn phải giữ đúng hợp
                // đồng KHÔNG throw, không chỉ riêng lỗi mạng ở catch bên dưới.
                _logger.LogError(ex, "Speedsms trả về nội dung không phải JSON hợp lệ khi gửi OTP.");
                return false;
            }

            // KHÔNG log số điện thoại — số điện thoại là PII.
            if (!string.Equals(result?.Status, SuccessStatus, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Speedsms từ chối gửi OTP: Status={Status}, Code={Code}.",
                    result?.Status ?? "(null)", result?.Code ?? "(null)");
                return false;
            }

            _logger.LogInformation("Đã gửi OTP qua Speedsms.");
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Hợp đồng của IOtpSmsService: KHÔNG ném ngoại lệ ra ngoài.
            _logger.LogError(ex, "Không gọi được Speedsms để gửi OTP.");
            return false;
        }
    }

    /// <summary>
    /// ASCII thuần (không dấu) — hầu hết brandname SMS trong nước tính phí gấp đôi số ký tự
    /// mỗi tin nếu dùng Unicode (70 ký tự/tin so với 160 ký tự/tin của GSM7 ASCII); một mã OTP
    /// ngắn không cần dấu để đọc được.
    /// </summary>
    private static string BuildMessage(string otpCode) =>
        $"ADSUS: Ma xac thuc dang ky cua ban la {otpCode}. Ma co hieu luc trong 5 phut. " +
        "Khong chia se ma nay cho bat ky ai.";

    private class SpeedsmsRequest
    {
        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        [JsonPropertyName("sms_type")] public string SmsType { get; set; } = string.Empty;
    }

    private class SpeedsmsResponse
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
    }
}
