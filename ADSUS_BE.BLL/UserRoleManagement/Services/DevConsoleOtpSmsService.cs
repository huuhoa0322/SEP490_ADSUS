using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Bản dự phòng của <see cref="IOtpSmsService"/> cho người chưa khai eSMS — rập khuôn
/// <see cref="DevConsoleEmailService"/>.
///
/// CHỈ DÙNG KHI PHÁT TRIỂN. Ngoài Development mà chưa khai <see cref="EsmsSettings"/> thì
/// ứng dụng dừng ngay lúc khởi động (xem Program.cs) — không ai lỡ đưa lên thật với SMS
/// không hoạt động.
///
/// In mã OTP ra console vì đây là cách rò rỉ ít nhất khi chưa có nhà cung cấp SMS thật: không
/// đi ra mạng, không trả về cho client, không lưu vào database (DB chỉ giữ bản hash).
/// </summary>
public class DevConsoleOtpSmsService : IOtpSmsService
{
    private readonly ILogger<DevConsoleOtpSmsService> _logger;

    public DevConsoleOtpSmsService(ILogger<DevConsoleOtpSmsService> logger) => _logger = logger;

    public Task<bool> SendOtpAsync(
        string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[NO SMS PROVIDER CONFIGURED] OTP for {PhoneNumber} is {OtpCode} " +
            "-- this line only appears in the Development environment.",
            phoneNumber,
            otpCode);

        return Task.FromResult(true);
    }
}
