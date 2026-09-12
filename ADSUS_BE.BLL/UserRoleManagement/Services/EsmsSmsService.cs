using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản hiện thực <see cref="IOtpSmsService"/> gửi qua eSMS.vn REST API — cùng phong cách
/// <see cref="SendGridEmailService"/>: không dùng SDK, chỉ 1 lời gọi HTTP.
///
/// Tài liệu API: SendMultipleMessage_V4_post_json. CodeResult "100" là thành công; mọi giá
/// trị khác là lỗi (sai ApiKey/SecretKey, hết quota, số điện thoại không hợp lệ theo eSMS...).
/// </summary>
public class EsmsSmsService : IOtpSmsService
{
    private const string ApiUrl = "http://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/";
    private const string SuccessCode = "100";

    /// <summary>SmsType "2" = Brandname, theo tài liệu eSMS — loại duy nhất phù hợp cho OTP nghiệp vụ.</summary>
    private const string BrandnameSmsType = "2";

    private readonly HttpClient _http;
    private readonly EsmsSettings _settings;
    private readonly ILogger<EsmsSmsService> _logger;

    public EsmsSmsService(
        IHttpClientFactory httpClientFactory,
        IOptions<EsmsSettings> settings,
        ILogger<EsmsSmsService> logger)
    {
        _http = httpClientFactory.CreateClient("Esms");
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(
        string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        var request = new EsmsRequest
        {
            Phone = phoneNumber,
            Content = BuildMessage(otpCode),
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            Brandname = _settings.Brandname,
            SmsType = BrandnameSmsType,
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(ApiUrl, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "eSMS trả lỗi HTTP khi gửi OTP: {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<EsmsResponse>(cancellationToken);

            // KHÔNG log CodeResult/ErrorMessage kèm số điện thoại — số điện thoại là PII.
            if (result?.CodeResult != SuccessCode)
            {
                _logger.LogError(
                    "eSMS từ chối gửi OTP: CodeResult={CodeResult}.", result?.CodeResult ?? "(null)");
                return false;
            }

            _logger.LogInformation("Đã gửi OTP qua eSMS.");
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Hợp đồng của IOtpSmsService: KHÔNG ném ngoại lệ ra ngoài.
            _logger.LogError(ex, "Không gọi được eSMS để gửi OTP.");
            return false;
        }
    }

    /// <summary>
    /// ASCII thuần (không dấu) — khác BuildBody của SendGridEmailService. Hầu hết brandname
    /// SMS trong nước tính phí gấp đôi số ký tự mỗi tin nếu dùng Unicode (70 ký tự/tin so với
    /// 160 ký tự/tin của GSM7 ASCII); một mã OTP ngắn không cần dấu để đọc được.
    /// </summary>
    private static string BuildMessage(string otpCode) =>
        $"ADSUS: Ma xac thuc dang ky cua ban la {otpCode}. Ma co hieu luc trong 5 phut. " +
        "Khong chia se ma nay cho bat ky ai.";

    private class EsmsRequest
    {
        [JsonPropertyName("Phone")] public string Phone { get; set; } = string.Empty;
        [JsonPropertyName("Content")] public string Content { get; set; } = string.Empty;
        [JsonPropertyName("ApiKey")] public string ApiKey { get; set; } = string.Empty;
        [JsonPropertyName("SecretKey")] public string SecretKey { get; set; } = string.Empty;
        [JsonPropertyName("Brandname")] public string Brandname { get; set; } = string.Empty;
        [JsonPropertyName("SmsType")] public string SmsType { get; set; } = string.Empty;
    }

    private class EsmsResponse
    {
        [JsonPropertyName("CodeResult")] public string? CodeResult { get; set; }
        [JsonPropertyName("ErrorMessage")] public string? ErrorMessage { get; set; }
    }
}
