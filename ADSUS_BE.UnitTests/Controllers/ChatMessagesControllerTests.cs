using System.Security.Claims;
using System.Text;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Models;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ADSUS_BE.UnitTests.Controllers;

public class ChatMessagesControllerTests
{
    private static async IAsyncEnumerable<ChatStreamEvent> ToAsyncEvents(IEnumerable<ChatStreamEvent> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static (ChatMessagesController controller, DefaultHttpContext httpContext, Mock<IChatService> chatService) CreateController(
        Guid? userId = null,
        string? acceptHeader = "text/event-stream",
        bool addStreamQuery = false)
    {
        var chatService = new Mock<IChatService>();
        var controller = new ChatMessagesController(chatService.Object);

        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new Claim(ClaimTypes.Role, "PATIENT"),
            }, "TestAuth"));
        }

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            httpContext.Request.Headers.Accept = acceptHeader;
        }

        if (addStreamQuery)
        {
            httpContext.Request.QueryString = new QueryString("?stream=true");
        }

        httpContext.Response.Body = new MemoryStream();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        return (controller, httpContext, chatService);
    }

    [Fact]
    public async Task Send_StreamingRequest_SetsSseHeadersAndWritesEvents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (controller, httpContext, chatService) = CreateController(userId, acceptHeader: "text/event-stream");

        var messageId = Guid.NewGuid();
        chatService.Setup(s => s.StreamMessageAsync(userId, "Xin chào", It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEvents(new ChatStreamEvent[]
            {
                new ChatThinkingEvent("thinking"),
                new ChatDeltaEvent("Chào "),
                new ChatDeltaEvent("bạn!"),
                new ChatDoneEvent(messageId, "Chào bạn!", "General", false),
            }));

        var request = new SendChatMessageRequest { Content = "Xin chào" };

        // Act
        var result = await controller.Send(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<EmptyResult>(result);
        Assert.Equal("text/event-stream; charset=utf-8", httpContext.Response.ContentType);
        Assert.Contains("no-cache", httpContext.Response.Headers["Cache-Control"].ToString());
        Assert.Contains("keep-alive", httpContext.Response.Headers["Connection"].ToString());
        Assert.Equal("no", httpContext.Response.Headers["X-Accel-Buffering"].ToString());

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Contains("event: thinking\ndata: {\"status\":\"thinking\"}\n\n", body);
        Assert.Contains("event: delta\ndata: {\"chunk\":\"Chào \"}\n\n", body);
        Assert.Contains("event: delta\ndata: {\"chunk\":\"bạn!\"}\n\n", body);
        Assert.Contains("event: done\ndata: {", body);
        Assert.Contains("\"content\":\"Chào bạn!\"", body);
    }

    [Fact]
    public async Task Send_SynchronousJsonRequest_ReturnsOkApiResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (controller, _, chatService) = CreateController(userId, acceptHeader: "application/json");

        var expectedResponse = new ChatMessageResponse
        {
            MessageId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Content = "Phản hồi JSON",
            CreatedAt = DateTime.UtcNow,
            IsSafetyResponse = false,
        };

        var request = new SendChatMessageRequest { Content = "Tin nhắn JSON" };
        chatService.Setup(s => s.SendMessageAsync(userId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await controller.Send(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ChatMessageResponse>>(okResult.Value);
        Assert.Equal(200, apiResponse.Code);
        Assert.Equal("Phản hồi JSON", apiResponse.Data?.Content);
    }

    [Fact]
    public async Task Send_UnauthorizedUser_Returns401()
    {
        // Arrange - user has no claims
        var (controller, _, _) = CreateController(userId: null);
        var request = new SendChatMessageRequest { Content = "Tin nhắn" };

        // Act
        var result = await controller.Send(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Send_EmptyContent_Returns400BadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (controller, _, _) = CreateController(userId);
        var request = new SendChatMessageRequest { Content = "   " };

        // Act
        var result = await controller.Send(request, TestContext.Current.CancellationToken);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.Equal(400, apiResponse.Code);
        Assert.Contains("không được để trống", apiResponse.Message);
    }

    [Fact]
    public async Task Send_ContentTooLong_Returns400BadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (controller, _, _) = CreateController(userId);
        var request = new SendChatMessageRequest { Content = new string('x', 1001) };

        // Act
        var result = await controller.Send(request, TestContext.Current.CancellationToken);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.Equal(400, apiResponse.Code);
        Assert.Contains("1000", apiResponse.Message);
    }

    [Fact]
    public async Task Send_ClientAbortsConnection_HandlesCancellationCleanly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (controller, _, chatService) = CreateController(userId);

        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<ChatStreamEvent> StreamWithCancel([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new ChatThinkingEvent();
            cts.Cancel();
            await Task.Yield();
            throw new OperationCanceledException(cts.Token);
        }

        chatService.Setup(s => s.StreamMessageAsync(userId, "Xin chào", It.IsAny<CancellationToken>()))
            .Returns((Guid _, string _, CancellationToken ct) => StreamWithCancel(ct));

        var request = new SendChatMessageRequest { Content = "Xin chào" };

        // Act
        var result = await controller.Send(request, cts.Token);

        // Assert
        Assert.IsType<EmptyResult>(result);
    }

    [Fact]
    public async Task Send_ServiceThrowsException_WritesErrorSseEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (controller, httpContext, chatService) = CreateController(userId);

        async IAsyncEnumerable<ChatStreamEvent> StreamWithError([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new ChatThinkingEvent();
            await Task.Yield();
            throw new InvalidOperationException("Unexpected service crash");
        }

        chatService.Setup(s => s.StreamMessageAsync(userId, "Xin chào", It.IsAny<CancellationToken>()))
            .Returns((Guid _, string _, CancellationToken ct) => StreamWithError(ct));

        var request = new SendChatMessageRequest { Content = "Xin chào" };

        // Act
        var result = await controller.Send(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<EmptyResult>(result);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Contains("event: error\ndata: {", body);
        Assert.Contains("STREAM_ERROR", body);
    }
}
