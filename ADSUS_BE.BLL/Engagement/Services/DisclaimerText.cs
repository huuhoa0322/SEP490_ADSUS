namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Static disclaimer texts cho Module 10 Chat.
/// GB-02: mọi assistant response LUÔN kèm DisclaimerText.General ghép vào content
/// trước khi lưu DB.
/// Safety response: trả DisclaimerText.Safety, KHÔNG gọi LLM.
/// </summary>
public static class DisclaimerText
{
    /// <summary>
    /// Miễn trừ trách nhiệm chung cho mọi phản hồi AI.
    /// Ghép vào cuối nội dung LLM trước khi lưu ASSISTANT message.
    /// </summary>
    public const string General =
        "\n\n---\n" +
        "⚠️ **Thông tin trên do AI sinh ra — chỉ mang tính tham khảo, không phải chỉ định chuyên môn.**\n" +
        "Luôn hỏi bác sĩ phụ trách trước khi áp dụng bất kỳ thay đổi nào về thuốc hoặc sinh hoạt.";

    /// <summary>
    /// Safety response — trả khi PsychologyTopicFilter phát hiện từ khóa nhạy cảm.
    /// KHÔNG gọi LLM, KHÔNG lưu message dưới dạng "do LLM sinh ra".
    /// </summary>
    public const string Safety =
        "ADSUS không hỗ trợ tư vấn tâm lý.\n\n" +
        "Trợ lý AI không có khả năng tư vấn hay xử lý các vấn đề cảm xúc. " +
        "Điều này là một phần thiết kế để bảo vệ bạn — vì chỉ chuyên gia mới có đủ năng lực hỗ trợ các tình huống này.\n\n" +
        "**Liên hệ chuyên gia:**\n" +
        "• Tổng đài Tư vấn Tâm lý Sức khỏe Sinh sản: **1900-XXXX** (miễn phí, 24/7)\n" +
        "• Đặt lịch trực tiếp với Bác sĩ phụ trách qua mục **Lịch khám**";
}
