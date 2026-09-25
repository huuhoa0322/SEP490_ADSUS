using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ADSUS_BE.BLL.Common;

public static partial class HtmlHelper
{
    private static readonly HtmlSanitizer _sanitizer = new();

    [GeneratedRegex("<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<\s*br\s*/?>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"</\s*(p|div|h[1-6])\s*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BlockCloseTagRegex();

    [GeneratedRegex(@"<\s*li\s*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LiOpenTagRegex();

    [GeneratedRegex(@"</\s*li\s*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LiCloseTagRegex();

    public static string StripToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Dùng HtmlSanitizer bóc sạch các tag nguy hiểm (<script>, <iframe>, <img> có event handlers)
        var sanitized = _sanitizer.Sanitize(input);

        // 2. Chuyển đổi các thẻ ngắt dòng/đoạn thành \n để bảo toàn bố cục văn bản
        var withBreaks = BrTagRegex().Replace(sanitized, "\n");
        withBreaks = BlockCloseTagRegex().Replace(withBreaks, "\n");
        withBreaks = LiOpenTagRegex().Replace(withBreaks, "• ");
        withBreaks = LiCloseTagRegex().Replace(withBreaks, "\n");

        // 3. Bóc các tag HTML an toàn còn lại
        var noTags = HtmlTagRegex().Replace(withBreaks, "");

        // 4. HtmlDecode để giữ nguyên vẹn ký hiệu y khoa (< 4.0, > 38.5) và entity HTML
        var decoded = WebUtility.HtmlDecode(noTags);

        // 5. Chuẩn hóa khoảng trắng từng dòng và loại bỏ dòng trống dư thừa
        var lines = decoded
            .Split('\n')
            .Select(l => WhitespaceRegex().Replace(l, " ").Trim())
            .Where(l => l.Length > 0);

        return string.Join("\n", lines);
    }

    public static string StripTags(string? html) => StripToPlainText(html);
}
