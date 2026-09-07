using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.IntegrationTests.AppointmentScheduling;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.PrescriptionAdherence;

public partial class MedicinesControllerIntegrationTests
{
    private readonly Mock<IMedicineService> _medicineService = new();
    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task SearchMedicines_AsDoctor_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Doctor);

        var keyword = "para";
        var mockResult = new List<MedicineResponse>
        {
            new MedicineResponse { MedicineId = Guid.NewGuid(), Name = "Paracetamol 500mg", Status = "ACTIVE" }
        };

        _medicineService.Setup(s => s.SearchMedicinesAsync(keyword, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var response = await client.GetAsync($"/api/v1/medicines?search={keyword}&limit=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var body = await response.Content.ReadFromJsonAsync<List<MedicineResponse>>();
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("Paracetamol 500mg", body[0].Name);
    }
    
    [Fact]
    public async Task SearchMedicines_AsPatient_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        // Act
        var response = await client.GetAsync("/api/v1/medicines?search=test");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SearchMedicines_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // No token

        // Act
        var response = await client.GetAsync("/api/v1/medicines?search=test");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMedicineService>();
                services.AddScoped(_ => _medicineService.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }
    [Fact]
    public async Task ActivateMedicine_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var id = Guid.NewGuid();
        _medicineService.Setup(s => s.ActivateMedicineAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PatchAsync($"/api/v1/medicines/{id}/activate", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _medicineService.Verify(s => s.ActivateMedicineAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
