using System.Net.Http.Headers;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ADSUS_BE.IntegrationTests.AppointmentScheduling;

/// <summary>
/// Helper class for creating authenticated HTTP clients in integration tests.
/// Pattern: Real app with mocked repositories to avoid touching the team database.
/// </summary>
public static class TestAuthHelper
{
    /// <summary>
    /// Creates an HTTP client with a valid JWT token for a user with the specified role.
    /// </summary>
    /// <param name="app">WebApplicationFactory for the app.</param>
    /// <param name="userRepo">Mocked IUserRepository for user lookup.</param>
    /// <param name="role">User role (Doctor, Patient, Nurse, Admin).</param>
    /// <param name="userId">Optional user ID. If null, a new GUID will be generated.</param>
    /// <returns>HttpClient with Bearer token in Authorization header.</returns>
    public static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> app,
        Mock<IUserRepository> userRepo,
        UserRole role,
        Guid? userId = null)
    {
        var user = new User
        {
            UserId = userId ?? Guid.NewGuid(),
            Phone = "0900000000",
            FullName = $"Test {role}",
            PasswordHash = "hash-not-used-in-tests",
            Role = role,
            Status = UserStatus.Active,
        };

        // AccountStatusJwtEvents reads user from repository on each request,
        // so the mock must return this user.
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        userRepo.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        using var scope = app.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Creates an HTTP client for a Doctor user.
    /// </summary>
    public static HttpClient CreateDoctorClient(
        WebApplicationFactory<Program> app,
        Mock<IUserRepository> userRepo,
        Guid? doctorId = null)
    {
        return CreateAuthenticatedClient(app, userRepo, UserRole.Doctor, doctorId);
    }

    /// <summary>
    /// Creates an HTTP client for a Patient user.
    /// </summary>
    public static HttpClient CreatePatientClient(
        WebApplicationFactory<Program> app,
        Mock<IUserRepository> userRepo,
        Guid? patientId = null)
    {
        return CreateAuthenticatedClient(app, userRepo, UserRole.Patient, patientId);
    }

    /// <summary>
    /// Creates an HTTP client for a Nurse user.
    /// </summary>
    public static HttpClient CreateNurseClient(
        WebApplicationFactory<Program> app,
        Mock<IUserRepository> userRepo,
        Guid? nurseId = null)
    {
        return CreateAuthenticatedClient(app, userRepo, UserRole.Nurse, nurseId);
    }

    /// <summary>
    /// Creates an HTTP client for an Admin user.
    /// </summary>
    public static HttpClient CreateAdminClient(
        WebApplicationFactory<Program> app,
        Mock<IUserRepository> userRepo,
        Guid? adminId = null)
    {
        return CreateAuthenticatedClient(app, userRepo, UserRole.Admin, adminId);
    }
}
