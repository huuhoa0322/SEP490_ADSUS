using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản hiện thực <see cref="IEmailService"/> gửi qua SendGrid REST API (HTTPS) — lựa chọn thứ
/// 3 bên cạnh <see cref="ResendEmailService"/>/<see cref="SmtpEmailService"/>, xem lý do ở
/// <see cref="SendGridSettings"/>.
///
/// Không dùng SDK chính thức của SendGrid — cùng lý do <see cref="ResendEmailService"/> không
/// dùng SDK của Resend: chỉ cần đúng 1 lời gọi HTTP.
/// </summary>
public class SendGridEmailService : IEmailService
{
    private const string ApiUrl = "https://api.sendgrid.com/v3/mail/send";

    private readonly HttpClient _http;
    private readonly SendGridSettings _settings;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<SendGridSettings> settings,
        ILogger<SendGridEmailService> logger)
    {
        _http = httpClientFactory.CreateClient("SendGrid");
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
                personalizations = new[]
                {
                    new { to = new[] { new { email = toEmail } } },
                },
                from = new { email = _settings.FromAddress, name = _settings.FromName },
                subject = "ADSUS — Mật khẩu tạm thời của bạn",
                content = new[]
                {
                    new { type = "text/plain", value = BuildBody(fullName, temporaryPassword) },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            // SendGrid trả 202 Accepted khi nhận thành công (không phải 200) — email được
            // xếp hàng gửi, không đồng nghĩa đã tới hộp thư người nhận.
            if (!response.IsSuccessStatusCode)
            {
                // KHÔNG log body: SendGrid trả lại nguyên văn payload lỗi, có thể phản chiếu
                // ngược địa chỉ người nhận — log status là đủ (401 = sai API key, 403 = FromAddress
                // chưa qua Single Sender Verification, 400 = payload sai định dạng).
                _logger.LogError(
                    "SendGrid returned an error while sending the temporary password to {Email}: HTTP {StatusCode}.",
                    toEmail, (int)response.StatusCode);
                return false;
            }

            _logger.LogInformation("Sent the temporary password to {Email} via SendGrid.", toEmail);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Hợp đồng của IEmailService: KHÔNG ném ngoại lệ ra ngoài.
            _logger.LogError(ex, "Could not reach SendGrid to send the temporary password to {Email}.", toEmail);
            return false;
        }
    }

    /// <summary>
    /// Nội dung thư — viết đúng tiếng Việt có dấu (P12 review Feature 1, 28/08/2026: bản cũ
    /// kế thừa từ <c>SmtpEmailService</c>/<c>ResendEmailService</c> đã xóa dùng tiếng Việt
    /// không dấu, không phù hợp cho nội dung gửi thẳng tới người dùng cuối).
    /// </summary>
    private static string BuildBody(string fullName, string temporaryPassword) =>
        $"""
         Xin chào {fullName},

         Mật khẩu tạm thời cho tài khoản ADSUS của bạn là:

             {temporaryPassword}

         Hãy đăng nhập bằng số điện thoại đã đăng ký và mật khẩu này. Hệ thống sẽ yêu cầu bạn
         đổi mật khẩu ngay ở lần đăng nhập đầu tiên.

         Nếu bạn không yêu cầu cấp lại mật khẩu, vui lòng báo ngay cho quản trị viên.

         --
         ADSUS
         Thư tự động, vui lòng không trả lời.
         """;
}
