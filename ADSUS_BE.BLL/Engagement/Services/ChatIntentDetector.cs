using ADSUS_BE.BLL.Engagement.Interfaces;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Intent categories cho chatbot — dùng để selective query data sources.
/// </summary>
public enum ChatIntent
{
    Greeting,
    Prescription,
    Appointment,
    CaseHistory,
    Allergy,
    Disease,
    HealthLog,
    Blog,
    General,
}

/// <summary>
/// Data sources mà aggregator có thể query.
/// </summary>
[Flags]
public enum DataSource
{
    None                = 0,
    BasicInfo           = 1 << 0,
    ActivePrescriptions = 1 << 1,
    TodayIntakes        = 1 << 2,
    UpcomingAppointments = 1 << 3,
    RecentCases         = 1 << 4,
    Allergies           = 1 << 5,
    Diseases            = 1 << 6,
    RecentHealthLogs    = 1 << 7,
    RecentBlogs         = 1 << 8,
}

/// <summary>
/// Result của intent detection.
/// </summary>
public sealed class IntentResult
{
    public required ChatIntent Intent { get; init; }
    public required DataSource TriggeredSources { get; init; }
}

/// <summary>
/// Keyword-based intent detector for patient chat messages.
/// Maps intent → which data sources the aggregator should query.
///
/// Strategy: priority scoring with top-keyword boost — fast, deterministic, no ML.
/// Top keywords (e.g. "blog", "lịch hẹn") get priority over general keywords
/// to resolve ambiguous cases correctly.
/// </summary>
public sealed class ChatIntentDetector : IIntentDetector
{
    // Top keywords: high-confidence signal, checked first (3 points each)
    private static readonly string[] TopPrescriptionKeywords =
    [
        "thuốc", "đơn thuốc", "uống thuốc", "quên uống thuốc",
        "liều lượng", "hướng dẫn uống", "thuốc hôm nay", "kê đơn",
    ];

    private static readonly string[] TopAppointmentKeywords =
    [
        "lịch hẹn", "đặt lịch", "đặt khám", "lịch khám", "hẹn khám",
        "gặp bác sĩ", "cuộc hẹn", "khi nào", "bao giờ", "appointment", "schedule",
    ];

    private static readonly string[] TopCaseHistoryKeywords =
    [
        "kết quả khám", "lịch sử khám", "khám gần nhất", "lần khám trước",
        "bệnh án", "hồ sơ bệnh án", "sổ khám", "bệnh sử",
        "xét nghiệm", "siêu âm", "chụp x-quang", "chẩn đoán",
    ];

    private static readonly string[] TopAllergyKeywords =
    [
        "dị ứng", "allergy", "allergic", "dị ứng thuốc",
        "mẫn cảm", "không dung nạp", "phản ứng phụ",
    ];

    private static readonly string[] TopDiseaseKeywords =
    [
        "bệnh nền", "bệnh mãn tính", "bệnh mãn", "tiền sử bệnh",
        "bệnh lý", "mắc bệnh gì", "có bệnh không",
        "đái tháo đường", "tiểu đường", "cao huyết áp", "huyết áp cao",
        "gan nhiễm mỡ", "thận yếu", "tim mạch",
    ];

    private static readonly string[] TopHealthLogKeywords =
    [
        "nhật ký sức khỏe", "nhật ký", "ghi triệu chứng",
        "theo dõi sức khỏe", "hôm qua", "tuần trước", "gần đây tôi",
        "sức khỏe hôm nay", "cân nặng", "mệt",
    ];

    // Top blog: "blog" itself is the strongest signal
    private static readonly string[] TopBlogKeywords =
    [
        "blog", "bài viết", "bài viết sức khỏe", "xem blog", "đọc bài viết",
        "tin tức sức khỏe", "kiến thức sức khỏe",
    ];

    // Additional keywords (1 point each)
    private static readonly string[] AdditionalPrescriptionKeywords =
    [
        "liều", "cách uống", "bôi thuốc", "thuốc uống",
        "thuốc bôi", "thuốc nhỏ mắt", "thuốc mỡ", "dung dịch uống",
        "dược", "prescription",
    ];

    private static readonly string[] AdditionalAppointmentKeywords =
    [
        "gọi điện đặt", "muốn khám", "khám bệnh", "bác sĩ khám", "lịch bác sĩ",
        "được khám",
    ];

    private static readonly string[] AdditionalCaseHistoryKeywords =
    [
        "bác sĩ kết luận", "kết luận", "kết quả", "bệnh gì",
    ];

    private static readonly string[] AdditionalHealthLogKeywords =
    [
        "triệu chứng", "tôi bị đau", "tôi cảm thấy",
        "mệt mỏi", "đau đầu", "ho", "sốt",
    ];

    private static readonly string[] AdditionalBlogKeywords =
    [
        "hướng dẫn sức khỏe", "chế độ ăn",
        "dinh dưỡng", "tập luyện", "cho tôi xem",
    ];

    // Greeting keywords (exact/prefix match, checked before scoring)
    private static readonly string[] GreetingKeywords =
    [
        "xin chào", "chào bạn", "chào bác sĩ", "chào buổi sáng",
        "chào buổi trưa", "chào buổi tối",
        "hello", "hi there", "hi",
    ];

    private static readonly char[] PunctuationChars = [',', '.', '?', '!', ';', ':'];

    /// <summary>
    /// Detect intent from user message (synchronous, no I/O).
    /// </summary>
    public IntentResult Detect(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new IntentResult { Intent = ChatIntent.General, TriggeredSources = DataSource.None };

        // Normalize: strip punctuation and lowercase
        var normalized = Normalize(message);

        if (IsGreeting(normalized))
            return new IntentResult { Intent = ChatIntent.Greeting, TriggeredSources = DataSource.None };

        var scores = ScoreIntents(normalized);
        var intent = ResolveTie(normalized, scores);
        if (intent == ChatIntent.General)
            return new IntentResult { Intent = ChatIntent.General, TriggeredSources = DataSource.None };

        var sources = MapIntentToSources(intent);
        return new IntentResult { Intent = intent, TriggeredSources = sources };
    }

    private static string Normalize(string message)
        => StripDiacritics(
            message.ToLowerInvariant().Trim().Replace(",", "").Replace(".", "")
                .Replace("?", "").Replace("!", "").Replace(";", "").Replace(":", ""));

    /// <summary>
    /// Strip Vietnamese diacritics (e.g. "thuốc" → "thuoc", "hôm nay" → "hom nay").
    ///
    /// Vì sao CẦN: user gõ không dấu (Telex OFF, IME bị strip, mobile keyboard)
    /// là chuyện thường — input normalized "thuoc" không Contains("thuốc") (có dấu)
    /// nên mọi keyword đều fail, dẫn đến false-positive (vd keyword ngắn "ho"
    /// trong AdditionalHealthLog match vào substring "ho" của "hom" → intent sai).
    ///
    /// Form chuẩn NFD + loại combining marks (U+0300..U+036F) là canonical Unicode
    /// normalization — đủ cho tiếng Việt, không cần custom dictionary.
    /// </summary>
    private static string StripDiacritics(string s)
    {
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static bool ContainsKeyword(string normalized, string keyword)
    {
        // Tìm keyword đã strip dấu trong input đã strip dấu, dùng word boundary
        // để tránh false-positive: keyword "ho" (AdditionalHealthLog) phải match từ
        // "ho" đứng riêng, không phải substring "ho" trong "hom nay" (hôm nay).
        var pattern = @"\b" + System.Text.RegularExpressions.Regex.Escape(StripDiacritics(keyword)) + @"\b";
        return System.Text.RegularExpressions.Regex.IsMatch(normalized, pattern);
    }

    private static bool IsGreeting(string normalized)
    {
        foreach (var kw in GreetingKeywords)
        {
            // So sánh với keyword đã strip dấu, vì input có thể gõ không dấu.
            var stripped = StripDiacritics(kw);
            if (!normalized.StartsWith(stripped))
                continue;
            var after = normalized[stripped.Length..].TrimStart();
            if (string.IsNullOrEmpty(after) || after.Split(' ').Length < 3)
                return true;
        }
        return false;
    }

    private static Dictionary<ChatIntent, int> ScoreIntents(string normalized)
    {
        var scores = new Dictionary<ChatIntent, int>
        {
            [ChatIntent.Prescription]  = Score(normalized, TopPrescriptionKeywords, 3)
                                       + Score(normalized, AdditionalPrescriptionKeywords, 1),
            [ChatIntent.Appointment]  = Score(normalized, TopAppointmentKeywords, 3)
                                      + Score(normalized, AdditionalAppointmentKeywords, 1),
            [ChatIntent.CaseHistory]  = Score(normalized, TopCaseHistoryKeywords, 3)
                                      + Score(normalized, AdditionalCaseHistoryKeywords, 1),
            [ChatIntent.Allergy]      = Score(normalized, TopAllergyKeywords, 3),
            [ChatIntent.Disease]      = Score(normalized, TopDiseaseKeywords, 3),
            [ChatIntent.HealthLog]    = Score(normalized, TopHealthLogKeywords, 3)
                                      + Score(normalized, AdditionalHealthLogKeywords, 1),
            [ChatIntent.Blog]         = Score(normalized, TopBlogKeywords, 3)
                                      + Score(normalized, AdditionalBlogKeywords, 1),
        };
        return scores;
    }

    /// <summary>
    /// Resolve ties between Allergy and Prescription: allergy keyword present → Allergy wins.
    /// Allergy queries take clinical priority over general drug questions.
    /// </summary>
    private static ChatIntent ResolveTie(string normalized, Dictionary<ChatIntent, int> scores)
    {
        var top = scores.MaxBy(kv => kv.Value);
        if (top.Value == 0) return ChatIntent.General;

        var ties = scores.Where(kv => kv.Value == top.Value).Select(kv => kv.Key).ToList();
        if (ties.Count <= 1) return top.Key;

        // Allergy + Prescription tie: "dị ứng với thuốc gì?" → Allergy wins
        if (ties.Contains(ChatIntent.Allergy) && ties.Contains(ChatIntent.Prescription))
        {
            if (ContainsAny(normalized, TopAllergyKeywords))
                return ChatIntent.Allergy;
            return ChatIntent.Prescription;
        }

        return top.Key;
    }

    private static bool ContainsAny(string normalized, string[] keywords)
    {
        foreach (var kw in keywords)
            if (ContainsKeyword(normalized, kw)) return true;
        return false;
    }

    private static int Score(string normalized, string[] keywords, int points)
    {
        int total = 0;
        foreach (var kw in keywords)
        {
            if (ContainsKeyword(normalized, kw))
                total += points;
        }
        return total;
    }

    private static DataSource MapIntentToSources(ChatIntent intent) => intent switch
    {
        ChatIntent.Prescription   => DataSource.ActivePrescriptions | DataSource.TodayIntakes,
        ChatIntent.Appointment   => DataSource.UpcomingAppointments,
        ChatIntent.CaseHistory   => DataSource.RecentCases | DataSource.Allergies | DataSource.Diseases,
        ChatIntent.Allergy       => DataSource.Allergies,
        ChatIntent.Disease       => DataSource.Diseases,
        ChatIntent.HealthLog     => DataSource.RecentHealthLogs,
        ChatIntent.Blog          => DataSource.RecentBlogs,
        _                        => DataSource.None,
    };

    /// <summary>Async wrapper for compatibility with test and future async callers.</summary>
    public Task<IntentResult> DetectAsync(string? message, CancellationToken ct = default)
        => Task.FromResult(Detect(message));
}
