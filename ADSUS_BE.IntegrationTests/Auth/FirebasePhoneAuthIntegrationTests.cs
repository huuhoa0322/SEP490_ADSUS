using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ADSUS_BE.IntegrationTests.Auth;

public class FirebasePhoneAuthIntegrationTests
{
    [Fact]
    public async Task CompleteRegistration_MismatchedPasswords_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = "whatever",
            fullName = "Nguyễn Thị Lan",
            password = "Password123",
            confirmPassword = "Different123",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteRegistration_EmptyFirebaseToken_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register/complete", new
        {
            firebaseIdToken = "",
            fullName = "Nguyễn Thị Lan",
            password = "Password123",
            confirmPassword = "Password123",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompletePasswordResetWithFirebase_MismatchedPasswords_Returns400()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password/complete-with-firebase", new
        {
            firebaseIdToken = "whatever",
            newPassword = "NewPassword123",
            confirmNewPassword = "Different123",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
