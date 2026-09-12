using System.Globalization;
using System.Security.Cryptography;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Sinh mã OTP 6 số cho luồng bệnh nhân tự đăng ký (xác thực số điện thoại).
///
/// Dùng RandomNumberGenerator chứ không dùng Random — cùng lý do <see cref="TemporaryPasswordGenerator"/>:
/// Random đoán được nếu biết thời điểm khởi tạo.
/// </summary>
public static class OtpCodeGenerator
{
    private const int Length = 6;
    private const int UpperBoundExclusive = 1_000_000; // 10^6 — đúng 6 chữ số

    /// <summary>Trả về chuỗi đúng 6 ký tự số, có thể có số 0 ở đầu (ví dụ "004821").</summary>
    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, UpperBoundExclusive)
            .ToString(new string('0', Length), CultureInfo.InvariantCulture);
}
