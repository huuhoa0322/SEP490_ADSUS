using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ADSUS_BE.BLL.Common;

public static class HtmlHelper
{
    private static readonly HtmlSanitizer _sanitizer = new();

    public static string StripToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Dùng HtmlSanitizer bóc sạch các tag nguy hiểm (<script>, <iframe>, <img> có event handlers)
        var sanitized = _sanitizer.Sanitize(input);

        // 2. Bóc các tag HTML an toàn còn lại sang khoảng trắng
        var noTags = Regex.Replace(sanitized, "<[^>]+>", " ");

        // 3. HtmlDecode để giữ nguyên vẹn ký hiệu y khoa (< 4.0, > 38.5) và entity HTML
        var decoded = WebUtility.HtmlDecode(noTags);

        // 4. Chuẩn hóa khoảng trắng
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    public static string StripTags(string? html) => StripToPlainText(html);
}
