namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Default implementation của <see cref="IPsychologyTopicFilter"/>. Case-insensitive
/// substring match với 15 keyword đã chốt ở CLAUDE.md §3.2.
///
/// Tại sao substring match (không dùng word boundary):
/// - User nhập câu tự nhiên tiếng Việt, có thể gõ không dấu ("tram cam") hoặc có dấu
///   ("trầm cảm"), có thể viết liền ("tựtử") hoặc tách ("tự tử")
/// - Word boundary regex phức tạp + dễ miss edge case "nghiện ngập" vs "nghiệp"
/// - Cost: false positive ("nghiện" trong "nghiệp vụ") — chấp nhận được vì safety > UX
///   trong lĩnh vực y tế. Handler có thể refine bằng allow-list sau nếu cần.
///
/// KHÔNG dùng LLM để filter (anti-pattern §7.1) — tốn token + thêm latency + có thể
/// miss khi LLM hallucinate.
/// </summary>
public sealed class PsychologyTopicFilter : IPsychologyTopicFilter
{
    /// <summary>
    /// 15 keyword theo CLAUDE.md §3.2. Mảng tĩnh — không cần reload từ DB vì hiếm khi
    /// đổi, và phải deterministic cho unit test.
    /// </summary>
    private static readonly string[] UnsafeTopics =
    {
        "trầm cảm",
        "tự tử",
        "tự hại",
        "tự làm hại",
        "hoảng loạn",
        "panic",
        "rối loạn lo âu",
        "ám ảnh sợ",
        "ptsd",
        "stress kéo dài",
        "muốn chết",
        "không muốn sống",
        "cắt tay",
        "nghiện",
        "cai nghiện",
    };

    /// <summary>
    /// Keyword đã sort theo length desc — đảm bảo "longest match first". Quan trọng vì
    /// "nghiện" là substring của "cai nghiện": nếu duyệt "nghiện" trước, "Tôi đang cai
    /// nghiện" sẽ trả "nghiện" thay vì "cai nghiện" (cụ thể hơn, dễ debug hơn).
    /// Static readonly + sort 1 lần ở type init — không tốn cost runtime.
    /// </summary>
    private static readonly string[] UnsafeTopicsByLengthDesc = UnsafeTopics
        .OrderByDescending(k => k.Length)
        .ToArray();

    public string? DetectUnsafeTopic(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return null;
        }

        // OrdinalIgnoreCase: nhanh, không phụ thuộc culture, đủ cho keyword cố định.
        foreach (var topic in UnsafeTopicsByLengthDesc)
        {
            if (userMessage.Contains(topic, StringComparison.OrdinalIgnoreCase))
            {
                return topic;
            }
        }

        return null;
    }
}