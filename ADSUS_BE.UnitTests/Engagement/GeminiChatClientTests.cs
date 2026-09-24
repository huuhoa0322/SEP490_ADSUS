using System.Net;
using System.Text;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.Engagement;

public class GeminiChatClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private static (GeminiChatClient client, Mock<IHttpClientFactory> factory) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage>? responseHandler = null,
        string? apiKey = "test-gemini-key",
        string model = "gemini-1.5-flash")
    {
        var factoryMock = new Mock<IHttpClientFactory>();

        if (responseHandler != null)
        {
            var handler = new MockHttpMessageHandler(responseHandler);
            var httpClient = new HttpClient(handler);
            factoryMock.Setup(f => f.CreateClient("AiBackend")).Returns(httpClient);
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ApiKey"] = apiKey,
                ["OpenAi:Model"] = model,
            })
            .Build();

        var logger = Mock.Of<ILogger<GeminiChatClient>>();
        var client = new GeminiChatClient(factoryMock.Object, logger, config);

        return (client, factoryMock);
    }

    [Fact]
    public async Task StreamMessageAsync_ValidSseResponse_YieldsAllChunks()
    {
        // Arrange
        var ssePayload =
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Xin \"}]}}]}\n\n" +
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"chào bạn!\"}]}}]}\n\n";

        var (client, _) = CreateClient(req =>
        {
            Assert.Contains("streamGenerateContent?alt=sse", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ssePayload, Encoding.UTF8, "text/event-stream")
            };
        });

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamMessageAsync(
            "System prompt",
            Array.Empty<ChatTurn>(),
            "Xin chào",
            TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.Equal("Xin ", chunks[0]);
        Assert.Equal("chào bạn!", chunks[1]);
    }

    [Fact]
    public async Task StreamMessageAsync_MissingApiKey_YieldsFallbackMessage()
    {
        // Arrange
        var (client, factory) = CreateClient(apiKey: null);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamMessageAsync(
            "System prompt",
            Array.Empty<ChatTurn>(),
            "Hello",
            TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Contains("Trợ lý AI hiện không khả dụng", chunks[0]);
        factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StreamMessageAsync_GeminiReturnsErrorStatus_YieldsFallback()
    {
        // Arrange
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":{\"code\":429,\"message\":\"Quota exceeded\"}}", Encoding.UTF8, "application/json")
        });

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamMessageAsync(
            "System prompt",
            Array.Empty<ChatTurn>(),
            "Hỏi câu tiếp",
            TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal("Trợ lý AI đang bận. Vui lòng thử lại sau.", chunks[0]);
    }

    [Fact]
    public async Task StreamMessageAsync_EmptyOrNullCandidates_SkipsEmptyChunks()
    {
        // Arrange
        var ssePayload =
            "data: {\"candidates\":[]}\n\n" +
            "data: {\"candidates\":[{\"content\":{\"parts\":[]}}]}\n\n" +
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Uống nhiều nước.\"}]}}]}\n\n";

        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ssePayload, Encoding.UTF8, "text/event-stream")
        });

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamMessageAsync(
            "System prompt",
            Array.Empty<ChatTurn>(),
            "Uống nước",
            TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal("Uống nhiều nước.", chunks[0]);
    }

    [Fact]
    public async Task StreamMessageAsync_SafetyFinishReason_YieldsSafetyNotice()
    {
        // Arrange
        var ssePayload =
            "data: {\"candidates\":[{\"content\":{\"parts\":[]},\"finishReason\":\"SAFETY\"}]}\n\n";

        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ssePayload, Encoding.UTF8, "text/event-stream")
        });

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamMessageAsync(
            "System prompt",
            Array.Empty<ChatTurn>(),
            "Nội dung nhạy cảm",
            TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Contains("tiêu chuẩn an toàn", chunks[0]);
    }

    [Fact]
    public async Task StreamMessageAsync_CancellationTokenAlreadyCancelled_YieldsNothing()
    {
        // Arrange
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"abc\"}]}}]}\n\n")
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in client.StreamMessageAsync(
            "System prompt",
            Array.Empty<ChatTurn>(),
            "Hủy",
            cts.Token))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Empty(chunks);
    }
}
