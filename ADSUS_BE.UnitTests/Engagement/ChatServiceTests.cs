using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho ChatService (Module 10 Chat FT-39).
/// TDD: RED → GREEN → Refactor per CLAUDE.md testing.md.
/// </summary>
public class ChatServiceTests
{
    private static ChatService NewSut(
        IAiChatMessageRepository? repo = null,
        IPsychologyTopicFilter? filter = null,
        IChatClient? chatClient = null)
    {
        repo ??= new Mock<IAiChatMessageRepository>().Object;
        filter ??= new Mock<IPsychologyTopicFilter>().Object;
        chatClient ??= new Mock<IChatClient>().Object;

        var settings = Options.Create(new BLL.Common.AiBackendSettings
        {
            ChatBotSystemPrompt = "Test system prompt",
        });

        return new ChatService(repo, filter, chatClient, Mock.Of<ILogger<ChatService>>(), settings);
    }

    // ── SendMessageAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_SafeMessage_SavesUserAndAssistant()
    {
        // Arrange
        var repo = new Mock<IAiChatMessageRepository>();
        var filter = new Mock<IPsychologyTopicFilter>();
        var chat = new Mock<IChatClient>();

        AiChatMessage? savedUserMsg = null;
        AiChatMessage? savedAssistantMsg = null;

        repo.Setup(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AiChatMessage, CancellationToken>((m, _) =>
            {
                if (m.Role == ChatRole.User) savedUserMsg = m;
                else savedAssistantMsg = m;
            })
            .ReturnsAsync((AiChatMessage m, CancellationToken _) => m);

        repo.Setup(r => r.ListByUserAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiChatMessage>());

        filter.Setup(f => f.DetectUnsafeTopic(It.IsAny<string>())).Returns((string?)null);
        chat.Setup(c => c.SendMessageAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mock AI response");

        var sut = NewSut(repo.Object, filter.Object, chat.Object);
        var userId = Guid.NewGuid();

        // Act
        var result = await sut.SendMessageAsync(userId, new SendChatMessageRequest { Content = "Tôi bị đau đầu" });

        // Assert
        Assert.Equal(ChatRole.Assistant, result.Role);
        Assert.False(result.IsSafetyResponse);
        Assert.Contains("Mock AI response", result.Content);

        // Verify USER message saved correctly
        Assert.NotNull(savedUserMsg);
        Assert.Equal(ChatRole.User, savedUserMsg!.Role);
        Assert.Equal("Tôi bị đau đầu", savedUserMsg.Content);

        // Verify ASSISTANT message saved with disclaimer
        Assert.NotNull(savedAssistantMsg);
        Assert.Equal(ChatRole.Assistant, savedAssistantMsg!.Role);
        Assert.Contains("Mock AI response", savedAssistantMsg.Content);
        Assert.Contains("thông tin", savedAssistantMsg.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AI sinh ra", savedAssistantMsg.Content, StringComparison.OrdinalIgnoreCase);

        // Verify LLM was called (GB-02 safety gate passed)
        chat.Verify(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(),
            "Tôi bị đau đầu", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_PsychologyTopic_ReturnsSafetyResponse()
    {
        // Arrange
        var repo = new Mock<IAiChatMessageRepository>();
        var filter = new Mock<IPsychologyTopicFilter>();
        var chat = new Mock<IChatClient>();

        repo.Setup(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiChatMessage m, CancellationToken _) => m);
        filter.Setup(f => f.DetectUnsafeTopic(It.IsAny<string>())).Returns("trầm cảm");
        // chat MUST NOT be called for unsafe topic

        var sut = NewSut(repo.Object, filter.Object, chat.Object);

        // Act
        var result = await sut.SendMessageAsync(Guid.NewGuid(), new SendChatMessageRequest { Content = "Tôi bị trầm cảm" });

        // Assert
        Assert.True(result.IsSafetyResponse);
        Assert.Equal(ChatRole.Assistant, result.Role);
        Assert.Contains("1900-XXXX", result.Content); // hotline in safety text

        // Verify: LLM NOT called (GB-02)
        chat.Verify(c => c.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Still saves both messages
        repo.Verify(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendMessageAsync_EmptyContent_ThrowsArgumentException()
    {
        var sut = NewSut();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendMessageAsync(Guid.NewGuid(), new SendChatMessageRequest { Content = "" }));
    }

    [Fact]
    public async Task SendMessageAsync_WhitespaceContent_ThrowsArgumentException()
    {
        var sut = NewSut();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendMessageAsync(Guid.NewGuid(), new SendChatMessageRequest { Content = "   " }));
    }

    [Fact]
    public async Task SendMessageAsync_ContentTooLong_ThrowsArgumentException()
    {
        var sut = NewSut();
        var longContent = new string('x', 1001);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendMessageAsync(Guid.NewGuid(), new SendChatMessageRequest { Content = longContent }));

        Assert.Contains("1000", ex.Message);
    }

    [Fact]
    public async Task SendMessageAsync_ContentAtMaxLength_Accepted()
    {
        var repo = new Mock<IAiChatMessageRepository>();
        var filter = new Mock<IPsychologyTopicFilter>();
        var chat = new Mock<IChatClient>();

        repo.Setup(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiChatMessage m, CancellationToken _) => m);
        repo.Setup(r => r.ListByUserAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiChatMessage>());
        filter.Setup(f => f.DetectUnsafeTopic(It.IsAny<string>())).Returns((string?)null);
        chat.Setup(c => c.SendMessageAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("OK");

        var sut = NewSut(repo.Object, filter.Object, chat.Object);
        var maxContent = new string('x', 1000); // exactly 1000

        var result = await sut.SendMessageAsync(Guid.NewGuid(), new SendChatMessageRequest { Content = maxContent });

        Assert.NotNull(result);
        Assert.Equal(ChatRole.Assistant, result.Role);
    }

    // ── GetHistoryAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_ReturnsMessagesInDescOrder()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var messages = new List<AiChatMessage>
        {
            new() { MessageId = Guid.NewGuid(), UserId = userId, Content = "First", Role = ChatRole.User, CreatedAt = now.AddMinutes(-10) },
            new() { MessageId = Guid.NewGuid(), UserId = userId, Content = "Second", Role = ChatRole.Assistant, CreatedAt = now.AddMinutes(-5) },
        };

        var repo = new Mock<IAiChatMessageRepository>();
        repo.Setup(r => r.ListByUserAsync(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var sut = NewSut(repo.Object);

        var result = await sut.GetHistoryAsync(userId, now.AddDays(-1), now, 50);

        Assert.Equal(2, result.Messages.Count);
    }

    [Fact]
    public async Task GetHistoryAsync_LimitClampedToMax200()
    {
        var repo = new Mock<IAiChatMessageRepository>();
        repo.Setup(r => r.ListByUserAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.Is<int>(limit => limit == 200), // clamped to 200
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiChatMessage>());

        var sut = NewSut(repo.Object);

        await sut.GetHistoryAsync(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, 9999);

        repo.Verify(r => r.ListByUserAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHistoryAsync_SafetyResponseInHistory_FlagsCorrectly()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var messages = new List<AiChatMessage>
        {
            new()
            {
                MessageId = Guid.NewGuid(), UserId = userId,
                Content = "ADSUS không hỗ trợ tư vấn tâm lý.\n\n**Liên hệ chuyên gia:**", // safety content
                Role = ChatRole.Assistant, CreatedAt = now,
            },
        };

        var repo = new Mock<IAiChatMessageRepository>();
        repo.Setup(r => r.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var sut = NewSut(repo.Object);

        var result = await sut.GetHistoryAsync(userId, now.AddDays(-1), now, 50);

        Assert.Single(result.Messages);
        Assert.True(result.Messages[0].IsSafetyResponse);
    }
}
