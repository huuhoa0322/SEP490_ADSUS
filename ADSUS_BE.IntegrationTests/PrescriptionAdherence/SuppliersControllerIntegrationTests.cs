using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
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

public class SuppliersControllerIntegrationTests
{
    private readonly Mock<ISupplierService> _supplierService = new();
    private readonly Mock<IUserRepository> _users = new();

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISupplierService>();
                services.AddScoped(_ => _supplierService.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    [Fact]
    public async Task GetSuppliers_AsAdmin_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var pagedResult = new PagedResult<SupplierResponse>(
            new List<SupplierResponse>
            {
                new SupplierResponse(Guid.NewGuid(), "Test", "123", "a@a", "A", "1", true, DateTime.UtcNow, DateTime.UtcNow)
            }, 1, 10, 1, 1);

        _supplierService.Setup(s => s.GetSuppliersAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var response = await client.GetAsync("/api/v1/suppliers?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<SupplierResponse>>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task GetSuppliers_AsDoctor_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Doctor);

        // Act
        var response = await client.GetAsync("/api/v1/suppliers");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSupplier_AsAdmin_ReturnsCreated()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new CreateSupplierRequest("New", "123", "a@a", "A", "1");
        var mockResponse = new SupplierResponse(Guid.NewGuid(), "New", "123", "a@a", "A", "1", true, DateTime.UtcNow, DateTime.UtcNow);

        _supplierService.Setup(s => s.CreateSupplierAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/suppliers", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateSupplier_MissingRequiredField_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        // Bỏ trống Name
        var request = new CreateSupplierRequest("", "123", "a@a", "A", "1");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/suppliers", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSupplier_NameAlreadyExists_ReturnsUnprocessableEntity()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new CreateSupplierRequest("Duplicate", "123", "a@a", "A", "1");

        _supplierService.Setup(s => s.CreateSupplierAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("Tên nhà cung cấp đã tồn tại."));

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/suppliers", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSupplier_AsAdmin_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);
        var id = Guid.NewGuid();

        var request = new UpdateSupplierRequest("Updated", "123", "a@a", "A", "1");
        var mockResponse = new SupplierResponse(id, "Updated", "123", "a@a", "A", "1", true, DateTime.UtcNow, DateTime.UtcNow);

        _supplierService.Setup(s => s.UpdateSupplierAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/suppliers/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSupplierStatus_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);
        var id = Guid.NewGuid();

        _supplierService.Setup(s => s.UpdateSupplierStatusAsync(id, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PatchAsJsonAsync($"/api/v1/suppliers/{id}/status", new { isActive = false });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSupplierStatus_NotFound_ReturnsNotFound()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);
        var id = Guid.NewGuid();

        _supplierService.Setup(s => s.UpdateSupplierStatusAsync(id, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("Not found"));

        // Act
        var response = await client.PatchAsJsonAsync($"/api/v1/suppliers/{id}/status", new { isActive = false });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
