using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản dự phòng của <see cref="IEmailService"/> cho người chưa khai máy chủ SMTP.
///
/// ====================================================================================
/// CHỈ DÙNG KHI PHÁT TRIỂN, và chỉ khi CHƯA khai SendGridSettings. Khai rồi thì Program.cs
/// dùng <see cref="SendGridEmailService"/>. Ngoài Development mà chưa khai thì ứng dụng dừng
/// ngay lúc khởi động — cố ý như vậy, để không ai lỡ đưa lên thật với email không hoạt động.
/// ====================================================================================
///
/// Vì sao phải in mật khẩu ra log: chưa cấu hình gửi mail thì không ai biết mật khẩu tạm,
/// mà không biết thì tài khoản vừa tạo hoàn toàn không dùng được — cả Module 2 không kiểm
/// thử nổi. In ra cửa sổ console của người đang chạy máy chủ là cách rò rỉ ít nhất: không
/// đi ra mạng, không trả về cho client, không lưu vào database.
/// </summary>
public class DevConsoleEmailService : IEmailService
{
    private readonly ILogger<DevConsoleEmailService> _logger;

    public DevConsoleEmailService(ILogger<DevConsoleEmailService> logger) => _logger = logger;

    public Task<bool> SendTemporaryPasswordAsync(
        string toEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[NO EMAIL SERVICE CONFIGURED] Temporary password for account {FullName} <{Email}>: {Password} " +
            "-- this line only appears in the Development environment.",
            fullName,
            toEmail,
            temporaryPassword);

        return Task.FromResult(true);
    }
}
