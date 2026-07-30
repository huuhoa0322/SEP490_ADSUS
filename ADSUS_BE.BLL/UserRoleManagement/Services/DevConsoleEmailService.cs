using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản thay thế tạm cho <see cref="IEmailService"/> trong lúc chưa có dịch vụ gửi mail thật.
///
/// ====================================================================================
/// CHỈ DÙNG KHI PHÁT TRIỂN. Program.cs chỉ đăng ký lớp này ở môi trường Development.
/// Chạy môi trường khác mà chưa có bản hiện thực thật thì ứng dụng sẽ báo lỗi ngay lúc
/// khởi động — cố ý như vậy, để không ai lỡ đưa lên thật với email không hoạt động.
/// ====================================================================================
///
/// Vì sao phải in mật khẩu ra log: chưa gửi được email thì không ai biết mật khẩu tạm, mà
/// không biết thì tài khoản vừa tạo hoàn toàn không dùng được — cả Module 2 không kiểm thử
/// nổi. In ra cửa sổ console của người đang chạy máy chủ là cách rò rỉ ít nhất: không đi ra
/// mạng, không trả về cho client, không lưu vào database.
///
/// Có dịch vụ mail thật rồi thì XOÁ lớp này đi, đừng để sót lên môi trường thật.
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
            "[CHUA CO DICH VU EMAIL] Mat khau tam cua tai khoan {FullName} <{Email}>: {Password} " +
            "-- dong nay chi xuat hien o moi truong Development.",
            fullName,
            toEmail,
            temporaryPassword);

        return Task.FromResult(true);
    }
}
