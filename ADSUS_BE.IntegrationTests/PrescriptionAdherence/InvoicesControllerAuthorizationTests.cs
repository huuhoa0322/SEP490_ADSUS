using System.Net;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.IntegrationTests.AppointmentScheduling;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.PrescriptionAdherence;

public class InvoicesControllerAuthorizationTests
{
    private readonly Mock<IInvoiceService> _invoiceService = new();
    private readonly Mock<IUserRepository> _users = new();

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInvoiceService>();
                services.AddScoped(_ => _invoiceService.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    [Fact]
    public async Task GetInvoices_AsNurse_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Nurse);

        var mockResult = new PagedResult<InvoiceResponse>(new List<InvoiceResponse>(), 1, 10, 0, 0);

        _invoiceService.Setup(s => s.GetInvoicesAsync(It.IsAny<InvoiceFilter>()))
            .ReturnsAsync(mockResult);

        // Act
        var response = await client.GetAsync("/api/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoices_AsPatient_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        // Act
        var response = await client.GetAsync("/api/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task GetInvoices_AsDoctor_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Doctor);

        // Act
        var response = await client.GetAsync("/api/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    
    [Fact]
    public async Task GetInvoices_AsAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
