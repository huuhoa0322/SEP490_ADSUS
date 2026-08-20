using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Fake implementation của IChatClient — dùng trong dev/test/CI khi không có OpenAI API key.
/// Trả về phản hồi tĩnh mẫu để test happy path và safety path.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private static readonly IReadOnlyList<string> MockResponses = new[]
    {
        "Dựa trên thông tin bạn cung cấp, đây là thông tin tham khảo chung về vấn đề bạn hỏi. " +
        "Tuy nhiên, mỗi tình huống sức khỏe là khác nhau, bạn nên tham khảo ý kiến bác sĩ để có kết luận chính xác nhất.",

        "Tôi hiểu câu hỏi của bạn. Vui lòng tham khảo thông tin trong mục Hồ sơ bệnh án hoặc " +
        "bài viết Blog Sức khỏe để biết thêm chi tiết. Nếu cần, hãy liên hệ bác sĩ phụ trách.",

        "Cảm ơn câu hỏi của bạn! Đây là thông tin tổng quan — để có hướng dẫn chính xác, " +
        "bạn nên trao đổi trực tiếp với bác sĩ đang theo dõi tình trạng của mình.",
    };

    private int _responseIndex;

    public Task<string> SendMessageAsync(
        string systemPrompt,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken ct = default)
    {
        // Fake: trả lời lần lượt theo round-robin để test nhiều response khác nhau.
        var response = MockResponses[_responseIndex % MockResponses.Count];
        _responseIndex++;
        return Task.FromResult(response);
    }
}
