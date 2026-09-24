using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Models;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Adversarial stress tests for Backend SSE Streaming pipeline (ChatService, GeminiChatClient, ChatMessagesController).
/// Executed by Challenger 1.
/// </summary>
public class BackendSseAdversarialTests
{
    private static ChatService CreateChatService(
        IAiChatMessageRepository repo,
        IPsychologyTopicFilter filter,
        IChatClient chatClient,
        IIntentDetector? intentDetector = null,
        IChatDataAggregator? aggregator = null)
    {
        intentDetector ??= Mock.Of<IIntentDetector>();
        aggregator ??= Mock.Of<IChatDataAggregator>();
        var settings = Options.Create(new AiBackendSettings
        {
            ChatBotSystemPrompt = "System Prompt",
        });

        return new ChatService(repo, filter, chatClient, intentDetector, aggregator, Mock.Of<ILogger<ChatService>>(), settings);
    }

    private static async IAsyncEnumerable<string> StreamChunks(IEnumerable<string> chunks)
    {
        foreach (var c in chunks)
        {
            yield return c;
            await Task.Yield();
        }
    }

    // ── Challenge 1: Cancellation mid-stream prevents partial DB save ──────────────

    [Fact]
    public async Task Challenge1_CancellationMidStream_NeverSavesPartialAssistantMessage()
    {
        var repo = new Mock<IAiChatMessageRepository>();
        var filter = new Mock<IPsychologyTopicFilter>();
        var chat = new Mock<IChatClient>();
        var intentDetector = new Mock<IIntentDetector>();

        var savedMessages = new List<AiChatMessage>();
        repo.Setup(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AiChatMessage, CancellationToken>((m, _) => savedMessages.Add(m))
            .ReturnsAsync((AiChatMessage m, CancellationToken _) => m);

        repo.Setup(r => r.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiChatMessage>());
        repo.Setup(r => r.CountAssistantMessagesSinceAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        filter.Setup(f => f.DetectUnsafeTopic(It.IsAny<string>())).Returns((string?)null);
        intentDetector.Setup(d => d.DetectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentResult { Intent = ChatIntent.General, TriggeredSources = DataSource.None });

        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<string> StreamAndAbort([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return "Đoạn 1...";
            // Client drops connection / closes browser mid-stream
            cts.Cancel();
            await Task.Yield();
            yield return "Đoạn 2 sau khi huỷ...";
        }

        chat.Setup(c => c.StreamMessageAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, IReadOnlyList<ChatTurn> _, string _, CancellationToken ct) => StreamAndAbort(ct));

        var sut = CreateChatService(repo.Object, filter.Object, chat.Object, intentDetector.Object);

        var eventsReceived = new List<ChatStreamEvent>();
        await foreach (var ev in sut.StreamMessageAsync(Guid.NewGuid(), "Bác sĩ ơi tư vấn giúp tôi", cts.Token))
        {
            eventsReceived.Add(ev);
        }

        // Must receive thinking and first delta, but stream must terminate cleanly
        Assert.Contains(eventsReceived, e => e is ChatThinkingEvent);
        Assert.Contains(eventsReceived, e => e is ChatDeltaEvent d && d.Chunk == "Đoạn 1...");
        Assert.DoesNotContain(eventsReceived, e => e is ChatDoneEvent);

        // Crucial invariant: Exactly 1 message in DB (USER). 0 ASSISTANT messages saved.
        Assert.Single(savedMessages);
        Assert.Equal(ChatRole.User, savedMessages[0].Role);
        Assert.DoesNotContain(savedMessages, m => m.Role == ChatRole.Assistant);
    }

    // ── Challenge 2: Psychology crisis strictly bypasses thinking & LLM ────────────

    [Fact]
    public async Task Challenge2_PsychologyCrisis_BypassesThinkingAndLlm_YieldsSingleDoneEvent()
    {
        var repo = new Mock<IAiChatMessageRepository>();
        var filter = new Mock<IPsychologyTopicFilter>();
        var chat = new Mock<IChatClient>();

        filter.Setup(f => f.DetectUnsafeTopic(It.IsAny<string>())).Returns("crisis-detected");

        var savedMessages = new List<AiChatMessage>();
        repo.Setup(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AiChatMessage, CancellationToken>((m, _) => savedMessages.Add(m))
            .ReturnsAsync((AiChatMessage m, CancellationToken _) => m);

        var sut = CreateChatService(repo.Object, filter.Object, chat.Object);

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in sut.StreamMessageAsync(Guid.NewGuid(), "Tôi quá mệt mỏi và muốn tự giải thoát", TestContext.Current.CancellationToken))
        {
            events.Add(ev);
        }

        // Must strictly yield exactly 1 event: ChatDoneEvent
        Assert.Single(events);
        Assert.IsType<ChatDoneEvent>(events[0]);
        var done = (ChatDoneEvent)events[0];

        // Must NOT emit thinking
        Assert.DoesNotContain(events, e => e is ChatThinkingEvent);
        // Must NOT emit delta
        Assert.DoesNotContain(events, e => e is ChatDeltaEvent);

        // Verification of safety properties
        Assert.True(done.IsSafetyResponse);
        Assert.Equal("PsychologyCrisis", done.DetectedIntent);
        Assert.Contains(DisclaimerText.Safety, done.Content);

        // Invariant: LLM is NEVER invoked
        chat.Verify(c => c.StreamMessageAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        chat.Verify(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // DB must have saved user and safety assistant message
        Assert.Equal(2, savedMessages.Count);
        Assert.Equal(ChatRole.User, savedMessages[0].Role);
        Assert.Equal(ChatRole.Assistant, savedMessages[1].Role);
        Assert.Equal(DisclaimerText.Safety, savedMessages[1].Content);
    }

    // ── Challenge 3: Rate limit >= 50 strictly bypasses thinking & LLM ─────────────

    [Theory]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(100)]
    public async Task Challenge3_RateLimitExceeded_BypassesThinkingAndLlm_YieldsSingleDoneEvent(int recentCalls)
    {
        var repo = new Mock<IAiChatMessageRepository>();
        var filter = new Mock<IPsychologyTopicFilter>();
        var chat = new Mock<IChatClient>();

        filter.Setup(f => f.DetectUnsafeTopic(It.IsAny<string>())).Returns((string?)null);
        repo.Setup(r => r.CountAssistantMessagesSinceAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentCalls);

        repo.Setup(r => r.AddAsync(It.IsAny<AiChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiChatMessage m, CancellationToken _) => m);

        var sut = CreateChatService(repo.Object, filter.Object, chat.Object);

        var events = new List<ChatStreamEvent>();
        await foreach (var ev in sut.StreamMessageAsync(Guid.NewGuid(), "Câu hỏi spam", TestContext.Current.CancellationToken))
        {
            events.Add(ev);
        }

        // Must strictly yield exactly 1 event: ChatDoneEvent
        Assert.Single(events);
        Assert.IsType<ChatDoneEvent>(events[0]);
        var done = (ChatDoneEvent)events[0];

        // Must NOT emit thinking or delta
        Assert.DoesNotContain(events, e => e is ChatThinkingEvent);
        Assert.DoesNotContain(events, e => e is ChatDeltaEvent);

        Assert.True(done.IsRateLimitExceeded);
        Assert.False(done.IsSafetyResponse);
        Assert.Contains("50 lượt hỏi", done.Content);

        // Invariant: LLM is NEVER invoked
        chat.Verify(c => c.StreamMessageAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Challenge 4: Gemini 429 quota exhaustion yields fallback without 500 ───────

    private sealed class DelegatingMockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _func;
        public DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> func) => _func = func;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) => Task.FromResult(_func(req));
    }

    [Fact]
    public async Task Challenge4_Gemini429QuotaExhaustion_YieldsFallbackGracefully()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("{\"error\":{\"code\":429,\"message\":\"Resource has been exhausted (e.g. check quota).\"}}", Encoding.UTF8, "application/json")
        });
        factoryMock.Setup(f => f.CreateClient("AiBackend")).Returns(new HttpClient(handler));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ApiKey"] = "fake-api-key",
                ["OpenAi:Model"] = "gemini-1.5-flash",
            })
            .Build();

        var geminiClient = new GeminiChatClient(factoryMock.Object, Mock.Of<ILogger<GeminiChatClient>>(), config);

        var chunks = new List<string>();
        await foreach (var chunk in geminiClient.StreamMessageAsync("sys", Array.Empty<ChatTurn>(), "user message", TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Must yield friendly fallback without unhandled exception
        Assert.Single(chunks);
        Assert.Equal("Trợ lý AI đang bận. Vui lòng thử lại sau.", chunks[0]);
    }

    // ── Challenge 5: Controller SSE flushing & headers ─────────────────────────────

    [Fact]
    public async Task Challenge5_Controller_SetsRequiredHeaders_FlushesEachEvent()
    {
        var userId = Guid.NewGuid();
        var chatService = new Mock<IChatService>();
        var controller = new ChatMessagesController(chatService.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "PATIENT"),
        }, "TestAuth"));
        httpContext.Request.Headers.Accept = "text/event-stream";

        var memoryStream = new MemoryStream();
        httpContext.Response.Body = memoryStream;

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var messageId = Guid.NewGuid();
        chatService.Setup(s => s.StreamMessageAsync(userId, "Xin chào bác sĩ", It.IsAny<CancellationToken>()))
            .Returns(StreamEvents(new ChatStreamEvent[]
            {
                new ChatThinkingEvent("thinking"),
                new ChatDeltaEvent("Chào bạn!"),
                new ChatDoneEvent(messageId, "Chào bạn!", "General", false),
            }));

        var result = await controller.Send(new SendChatMessageRequest { Content = "Xin chào bác sĩ" }, TestContext.Current.CancellationToken);

        Assert.IsType<EmptyResult>(result);

        // Check required SSE headers per PROJECT.md § Interface Contracts
        Assert.Equal("text/event-stream; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal("no-cache, no-transform", httpContext.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("keep-alive", httpContext.Response.Headers["Connection"].ToString());
        Assert.Equal("no", httpContext.Response.Headers["X-Accel-Buffering"].ToString());

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        // Verify SSE protocol compliance: event: <name>\ndata: <json>\n\n
        Assert.Contains("event: thinking\ndata: {\"status\":\"thinking\"}\n\n", body);
        Assert.Contains("event: delta\ndata: {\"chunk\":\"Chào bạn!\"}\n\n", body);
        Assert.Contains("event: done\ndata: {", body);
        Assert.Contains($"\"messageId\":\"{messageId}\"", body);
    }

    private static async IAsyncEnumerable<ChatStreamEvent> StreamEvents(IEnumerable<ChatStreamEvent> events)
    {
        foreach (var e in events)
        {
            yield return e;
            await Task.Yield();
        }
    }
}
