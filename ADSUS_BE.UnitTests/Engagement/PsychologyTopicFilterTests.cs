using ADSUS_BE.BLL.Engagement.Services;
using Xunit;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho PsychologyTopicFilter — safety check trước khi gọi LLM (Module 10 Chat).
/// Theo CLAUDE.md §3.2 + GB-02: AI không thay thế bác sĩ tâm lý. Nếu user input chứa
/// từ khóa tâm lý nhạy cảm → trả safety response (text có sẵn), KHÔNG gọi LLM.
///
/// Keyword list (chốt theo CLAUDE.md §3.2): trầm cảm, tự tử, tự hại, tự làm hại,
/// hoảng loạn, panic, rối loạn lo âu, ám ảnh sợ, PTSD, stress kéo dài, muốn chết,
/// không muốn sống, cắt tay, nghiện, cai nghiện.
///
/// Convention: keyword phải match có dấu ("trầm cảm"). Bệnh nhân Việt Nam nhập
/// có dấu trên mobile (bàn phím Tiếng Việt mặc định). KHÔNG support dấu-skip
/// (gõ "tram cam") trong scope hiện tại — sẽ refine bằng Unicode normalization nếu
/// cần ở sprint sau.
/// </summary>
public class PsychologyTopicFilterTests
{
    private readonly IPsychologyTopicFilter _filter = new PsychologyTopicFilter();

    // ---------- POSITIVE CASES — phát hiện đúng từ khóa ----------

    [Theory]
    [InlineData("Tôi đang bị trầm cảm", "trầm cảm")]
    [InlineData("tôi muốn tự tử", "tự tử")]
    [InlineData("Tôi muốn chết", "muốn chết")]
    [InlineData("Tôi không muốn sống nữa", "không muốn sống")]
    [InlineData("tôi hay tự làm hại bản thân", "tự làm hại")]
    [InlineData("Tôi hay cắt tay khi stress", "cắt tay")]
    [InlineData("Tôi hay bị hoảng loạn", "hoảng loạn")]
    [InlineData("Tôi hay bị panic", "panic")]
    [InlineData("Tôi bị rối loạn lo âu", "rối loạn lo âu")]
    [InlineData("Tôi bị ám ảnh sợ", "ám ảnh sợ")]
    [InlineData("Tôi bị PTSD sau tai nạn", "ptsd")]
    [InlineData("Tôi bị stress kéo dài", "stress kéo dài")]
    [InlineData("Tôi đang nghiện rượu", "nghiện")]
    [InlineData("Tôi đang cai nghiện", "cai nghiện")]
    public void DetectUnsafeTopic_MatchesKeywords_ReturnsTopic(string input, string expectedTopic)
    {
        // Act
        var result = _filter.DetectUnsafeTopic(input);

        // Assert
        Assert.Equal(expectedTopic, result);
    }

    // ---------- NEGATIVE CASES — input an toàn, cho phép gọi LLM ----------

    [Theory]
    [InlineData("Tôi bị đau đầu 3 ngày nay")]
    [InlineData("Tôi muốn đặt lịch khám bác sĩ")]
    [InlineData("Paracetamol uống như thế nào?")]
    [InlineData("Tôi bị sốt và ho")]
    [InlineData("Bệnh tiểu đường có nguy hiểm không?")]
    [InlineData("Tôi đang tập gym bị đau lưng")]
    public void DetectUnsafeTopic_AllowsSafeInput_ReturnsNull(string input)
    {
        // Act
        var result = _filter.DetectUnsafeTopic(input);

        // Assert
        Assert.Null(result);
    }

    // ---------- EDGE CASES ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DetectUnsafeTopic_EmptyOrWhitespace_ReturnsNull(string? input)
    {
        // Act
        var result = _filter.DetectUnsafeTopic(input!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DetectUnsafeTopic_CaseInsensitive_StillDetects()
    {
        // Arrange — viết hoa thường đều phải bắt được
        var input = "TÔI ĐANG BỊ TRẦM CẢM";

        // Act
        var result = _filter.DetectUnsafeTopic(input);

        // Assert
        Assert.Equal("trầm cảm", result);
    }

    [Fact]
    public void DetectUnsafeTopic_KeywordInMiddleOfWord_StillDetects()
    {
        // Arrange — "nghiện" xuất hiện trong "nghiện ngập" — substring match là đủ
        // (user nhập câu tự nhiên, có thể gõ "toi bi nghien ngu" — match "nghiện" vẫn OK)
        var input = "Tôi đang nghiện ngập";

        // Act
        var result = _filter.DetectUnsafeTopic(input);

        // Assert
        Assert.Equal("nghiện", result);
    }
}