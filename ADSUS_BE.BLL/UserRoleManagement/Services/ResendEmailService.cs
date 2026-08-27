using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản hiện thực <see cref="IEmailService"/> gửi qua Resend REST API (HTTPS), thay cho
/// <see cref="SmtpEmailService"/> — xem lý do đổi ở <see cref="ResendSettings"/>.
///
/// Không dùng SDK chính thức của Resend: chỉ cần đúng 1 lời gọi HTTP, thêm cả 1 gói để đổi
/// lấy 1 request là không đáng — cùng lý do <see cref="SupabaseStorageService"/> ở DAL không
/// dùng SDK Supabase.
/// </summary>
public class ResendEmailService : IEmailService
{
    private const string ApiUrl = "https://api.resend.com/emails";

    private readonly HttpClient _http;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<ResendSettings> settings,
        ILogger<ResendEmailService> logger)
    {
        _http = httpClientFactory.CreateClient("Resend");
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendTemporaryPasswordAsync(
        string toEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = JsonContent.Create(new
            {
                from = $"{_settings.FromName} <{_settings.FromAddress}>",
                to = new[] { toEmail },
                subject = "ADSUS — Mat khau tam thoi cua ban",
                text = BuildBody(fullName, temporaryPassword),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // KHÔNG log body: Resend trả lại nguyên văn payload lỗi, có thể phản chiếu
                // ngược địa chỉ người nhận — log status là đủ để chẩn đoán (401 = sai API key,
                // 403 = domain From chưa verify, 422 = payload sai định dạng).
                _logger.LogError(
                    "Resend tra ve loi khi gui mat khau tam toi {Email}: HTTP {StatusCode}.",
                    toEmail, (int)response.StatusCode);
                return false;
            }

            _logger.LogInformation("Da gui mat khau tam toi {Email} qua Resend.", toEmail);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Hợp đồng của IEmailService: KHÔNG ném ngoại lệ ra ngoài.
            _logger.LogError(ex, "Khong ket noi duoc toi Resend de gui mat khau tam toi {Email}.", toEmail);
            return false;
        }
    }

    /// <summary>Nội dung thư — y hệt <see cref="SmtpEmailService"/>, cố ý không chèn link.</summary>
    private static string BuildBody(string fullName, string temporaryPassword) =>
        $"""
         Xin chao {fullName},

         Mat khau tam thoi cho tai khoan ADSUS cua ban la:

             {temporaryPassword}

         Hay dang nhap bang so dien thoai da dang ky va mat khau nay. He thong se yeu cau ban
         doi mat khau ngay o lan dang nhap dau tien.

         Neu ban khong yeu cau cap lai mat khau, vui long bao ngay cho quan tri vien.

         --
         ADSUS
         Thu tu dong, vui long khong tra loi.
         """;
}
