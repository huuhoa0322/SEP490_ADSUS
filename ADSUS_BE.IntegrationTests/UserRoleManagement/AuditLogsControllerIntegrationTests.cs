using System.Net;
using System.Net.Http.Headers;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ADSUS_BE.IntegrationTests.UserRoleManagement;

public class AuditLogsControllerIntegrationTests
{
    private const string AuditLogsPath = "/api/v1/admin/audit-logs";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();

    [Fact]
    public async Task GetAuditLogs_AsAdmin_ReturnsOk()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        _auditLogs.Setup(r => r.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogEntry>(), 0));

        var response = await client.GetAsync(AuditLogsPath + "?page=1&pageSize=15", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLogs_BackwardCompatibilityLimit_ReturnsOk()
    {
        using var app = CreateApp();
        var client = CreateClient(app, UserRole.Admin);

        _auditLogs.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        var response = await client.GetAsync(AuditLogsPath + "?limit=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Doctor)]
    [InlineData(UserRole.Staff)]
    [InlineData(UserRole.Patient)]
    public async Task GetAuditLogs_NonAdmin_IsForbidden(UserRole role)
    {
        using var app = CreateApp();
        var client = CreateClient(app, role);

        var response = await client.GetAsync(AuditLogsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLogs_NoToken_IsUnauthorized()
    {
        using var app = CreateApp();
        var client = app.CreateClient();

        var response = await client.GetAsync(AuditLogsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
                services.RemoveAll<IAuditLogRepository>();
                services.AddScoped(_ => _auditLogs.Object);
            });
        });
    }

    private HttpClient CreateClient(WebApplicationFactory<Program> app, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000001",
            FullName = "Test User",
            PasswordHash = "hash",
            Role = role,
            Status = UserStatus.Active,
        };

        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        _users.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
