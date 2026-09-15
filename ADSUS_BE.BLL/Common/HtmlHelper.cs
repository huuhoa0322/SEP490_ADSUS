using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ADSUS_BE.BLL.Common;

public static partial class HtmlHelper
{
    private static readonly HtmlSanitizer _sanitizer = new();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string StripToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Dùng HtmlSanitizer bóc sạch các tag nguy hiểm (<script>, <iframe>, <img> có event handlers)
        var sanitized = _sanitizer.Sanitize(input);

        // 2. Bóc các tag HTML an toàn còn lại sang khoảng trắng
        var noTags = HtmlTagRegex().Replace(sanitized, " ");

        // 3. HtmlDecode để giữ nguyên vẹn ký hiệu y khoa (< 4.0, > 38.5) và entity HTML
        var decoded = WebUtility.HtmlDecode(noTags);

        // 4. Chuẩn hóa khoảng trắng
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    public static string StripTags(string? html) => StripToPlainText(html);
}
