using System.Security.Cryptography;
using System.Text;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Sinh mật khẩu tạm cho UC-04 BR-03 và UC-03 BR-02.
/// </summary>
public static class TemporaryPasswordGenerator
{
    // Cố tình bỏ các ký tự dễ đọc nhầm: O và 0, I, l và 1. Người dùng nhận mật khẩu qua
    // email rồi gõ tay, nhầm một ký tự là mất thêm một vòng hỏi đáp với phòng khám.
    private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijkmnpqrstuvwxyz";
    private const string DigitChars = "23456789";
    private const int Length = 12;

    /// <summary>
    /// Trả về mật khẩu ngẫu nhiên chắc chắn thoả chính sách ở TDS §4.3:
    /// 8–72 ký tự, ít nhất 1 chữ hoa, ít nhất 1 chữ số.
    ///
    /// Bảo đảm bằng cách đặt sẵn một chữ hoa và một chữ số rồi mới trộn, thay vì sinh ngẫu
    /// nhiên hết rồi kiểm tra lại — cách kia có xác suất phải sinh lại nhiều lần.
    ///
    /// Dùng RandomNumberGenerator chứ không dùng Random: Random đoán được nếu biết thời điểm
    /// khởi tạo, mà đây là mật khẩu cấp cho tài khoản y tế.
    /// </summary>
    public static string Generate()
    {
        var all = UpperChars + LowerChars + DigitChars;
        var chars = new char[Length];

        chars[0] = Pick(UpperChars);
        chars[1] = Pick(DigitChars);
        for (var i = 2; i < Length; i++) chars[i] = Pick(all);

        Shuffle(chars);

        return new string(chars);
    }

    private static char Pick(string source) =>
        source[RandomNumberGenerator.GetInt32(source.Length)];

    /// <summary>Trộn Fisher–Yates, để chữ hoa và chữ số không luôn nằm ở hai vị trí đầu.</summary>
    private static void Shuffle(char[] chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
