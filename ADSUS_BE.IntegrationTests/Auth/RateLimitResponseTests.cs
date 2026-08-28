using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.Auth;

/// <summary>
/// Kiểm tra phản hồi do chính middleware rate limit tạo ra, không chỉ status code.
/// Nếu thiếu envelope JSON, frontend không phân biệt được 429 với đăng nhập sai.
/// </summary>
public class RateLimitResponseTests
{
    private const string LoginPath = "/api/v1/auth/login";
    private const string RateLimitMessage =
        "Too many requests. Please wait before trying again.";

    [Fact]
    public async Task EleventhRequest_Returns429WithStandardJsonEnvelope()
    {
        using var app = CreateApp();
        using var client = app.CreateClient();
        var request = new LoginRequest
        {
            PhoneNumber = "0900000001",
            Password = "Aa123456@",
        };

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var allowedResponse = await client.PostAsJsonAsync(LoginPath, request);
            Assert.Equal(HttpStatusCode.Unauthorized, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsJsonAsync(LoginPath, request);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.Equal("application/json", rejectedResponse.Content.Headers.ContentType?.MediaType);
        Assert.True(rejectedResponse.Headers.Contains("Retry-After"));

        var body = await rejectedResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.Equal(StatusCodes.Status429TooManyRequests, body.Code);
        Assert.Equal(RateLimitMessage, body.Message);
        Assert.Null(body.Data);
    }

    private static WebApplicationFactory<Program> CreateApp()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(service => service.LoginAsync(
                It.IsAny<LoginRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginResponse?)null);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddScoped(_ => auth.Object);
            });
        });
    }
}
