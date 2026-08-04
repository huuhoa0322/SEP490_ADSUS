using System.Net;
using System.Net.Mail;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản hiện thực thật của <see cref="IEmailService"/> — gửi qua SMTP (API-04 trong PRD).
///
/// Dùng <see cref="SmtpClient"/> có sẵn trong .NET thay vì MailKit. Lý do: bản MailKit mới
/// nhất tại thời điểm viết (4.15.0) vẫn đang mang một cảnh báo bảo mật chưa có bản vá
/// (GHSA-9j88-vvj5-vhgr), mà thêm một gói bị gắn cờ vào đúng phần xử lý mật khẩu thì không
/// đáng. SmtpClient nằm sẵn trong .NET, hỗ trợ STARTTLS ở cổng 587 và async — đủ cho nhu cầu
/// của hệ thống này là gửi vài lá thư văn bản thuần.
///
/// Nếu sau này cần gửi hàng loạt, thư HTML phức tạp hay OAuth2 thì hãy chuyển sang MailKit —
/// lúc đó chỉ phải thay lớp này, phần còn lại không đụng tới.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendTemporaryPasswordAsync(
        string toEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        var from = string.IsNullOrWhiteSpace(_settings.FromAddress)
            ? _settings.Username
            : _settings.FromAddress;

        using var message = new MailMessage
        {
            From = new MailAddress(from, _settings.FromName),
            Subject = "ADSUS — Mat khau tam thoi cua ban",
            Body = BuildBody(fullName, temporaryPassword),
            // Thư văn bản thuần: không có link nào để bấm nên không tiếp tay cho lừa đảo,
            // và không lệ thuộc vào việc trình đọc mail có dựng HTML hay không.
            IsBodyHtml = false,
        };

        message.To.Add(new MailAddress(toEmail, fullName));

        try
        {
            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.UseStartTls,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                Timeout = _settings.TimeoutSeconds * 1000,
            };

            await client.SendMailAsync(message, cancellationToken);

            // Ghi log là ĐÃ GỬI TỚI AI, tuyệt đối không ghi mật khẩu — log thường được thu
            // gom về nơi khác và nhiều người đọc được.
            _logger.LogInformation("Da gui mat khau tam toi {Email}.", toEmail);

            return true;
        }
        catch (Exception ex)
        {
            // Hợp đồng của IEmailService: KHÔNG ném ngoại lệ. Bên gọi dựa vào giá trị trả về
            // để quyết định, ném ra là làm hỏng cả thao tác vì một lỗi ngoài tầm kiểm soát.
            _logger.LogError(
                ex,
                "Khong gui duoc mat khau tam toi {Email} qua {Host}:{Port}.",
                toEmail,
                _settings.SmtpHost,
                _settings.SmtpPort);

            return false;
        }
    }

    /// <summary>
    /// Nội dung thư. Cố ý không chèn đường dẫn đăng nhập: thư có chứa mật khẩu mà lại kèm
    /// link thì trông y hệt thư lừa đảo, và người dùng quen bấm link trong thư là đúng thói
    /// quen mà kẻ tấn công cần.
    /// </summary>
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
