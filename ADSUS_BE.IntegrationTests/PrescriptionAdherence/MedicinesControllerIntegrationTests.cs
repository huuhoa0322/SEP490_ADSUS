using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.IntegrationTests.AppointmentScheduling;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.PrescriptionAdherence;

public class MedicinesControllerIntegrationTests
{
    private readonly Mock<IMedicineRepository> _medicines = new();
    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task SearchMedicines_AsDoctor_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Doctor);

        var keyword = "para";
        var mockResult = new List<Medicine>
        {
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol 500mg" }
        };

        _medicines.Setup(r => r.SearchByNameAsync(keyword, 20, It.IsAny<CancellationToken>()))
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
                services.RemoveAll<IMedicineRepository>();
                services.AddScoped(_ => _medicines.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }
}
