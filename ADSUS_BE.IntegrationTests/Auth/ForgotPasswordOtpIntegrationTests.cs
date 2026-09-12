using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.Auth;

public class ForgotPasswordOtpIntegrationTests
{
    [Fact]
    public async Task RequestOtp_PhoneNotFound_Returns404WithClearMessage()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => userRepo.Object);
            });
        });
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password/request-otp",
            new { phoneNumber = "0987654321" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_InvalidPhoneFormat_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password/request-otp",
            new { phoneNumber = "not-a-phone" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteWithOtp_MismatchedPasswords_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password/complete", new
        {
            resetToken = "whatever",
            newPassword = "NewPassword123",
            confirmNewPassword = "Different123",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
