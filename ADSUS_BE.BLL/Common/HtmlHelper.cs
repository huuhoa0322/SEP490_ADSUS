using System.Net;
using System.Text.RegularExpressions;

namespace ADSUS_BE.BLL.Common;

public static class HtmlHelper
{
    public static string StripTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
