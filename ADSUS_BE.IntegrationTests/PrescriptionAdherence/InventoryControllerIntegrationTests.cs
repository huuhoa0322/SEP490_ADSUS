using System.Net;
using System.Net.Http.Json;
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

public class InventoryControllerIntegrationTests
{
    private readonly Mock<IInventoryService> _inventoryService = new();
    private readonly Mock<IUserRepository> _users = new();

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInventoryService>();
                services.AddScoped(_ => _inventoryService.Object);
                
                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    [Fact]
    public async Task ImportMedicineBulk_AsAdmin_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var requests = new List<ImportInventoryRequest>
        {
            new ImportInventoryRequest 
            { 
                MedicineId = Guid.NewGuid(),
                SupplierId = Guid.NewGuid(),
                MedicinePackagingId = Guid.NewGuid(),
                LotNumber = "LOT123",
                ExpiryDate = DateTime.UtcNow.AddDays(10),
                Quantity = 10,
                ImportPricePerUnit = 1000
            }
        };

        _inventoryService.Setup(s => s.ImportMedicineBulkAsync(It.IsAny<List<ImportInventoryRequest>>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/inventory/import/bulk", requests);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _inventoryService.Verify(s => s.ImportMedicineBulkAsync(It.IsAny<List<ImportInventoryRequest>>()), Times.Once);
    }

    [Fact]
    public async Task ValidateImport_AsAdmin_ReturnsOkWithResult()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new ImportInventoryRequest 
        { 
            MedicineId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            MedicinePackagingId = Guid.NewGuid(),
            LotNumber = "LOT123",
            ExpiryDate = DateTime.UtcNow.AddDays(10),
            Quantity = 10,
            ImportPricePerUnit = 1000
        };
        var mockResponse = new ImportValidationResponse { IsValid = false, ErrorMessage = "Test Error" };

        _inventoryService.Setup(s => s.ValidateImportAsync(It.IsAny<ImportInventoryRequest>()))
            .ReturnsAsync(mockResponse);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/inventory/validate-import", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ImportValidationResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsValid);
        Assert.Equal("Test Error", body.ErrorMessage);
    }

    [Fact]
    public async Task ImportMedicineBulk_AsPatient_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/inventory/import/bulk", new List<ImportInventoryRequest>());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdjustInventory_AsAdmin_ReturnsOk()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new AdjustInventoryRequest
        {
            BatchId = Guid.NewGuid(),
            NewQuantityBase = 50,
            Reason = "Kiểm kê đầu tháng"
        };

        var mockResponse = new AdjustInventoryResponse
        {
            TransactionId = Guid.NewGuid(),
            PreviousQuantity = 45,
            NewQuantity = 50,
            Delta = 5
        };

        _inventoryService.Setup(s => s.AdjustAsync(It.IsAny<AdjustInventoryRequest>()))
            .ReturnsAsync(mockResponse);

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/inventory/adjust", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdjustInventoryResponse>();
        Assert.NotNull(body);
        Assert.Equal(50, body.NewQuantity);
        _inventoryService.Verify(s => s.AdjustAsync(It.IsAny<AdjustInventoryRequest>()), Times.Once);
    }

    [Fact]
    public async Task AdjustInventory_AsPatient_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/inventory/adjust", new AdjustInventoryRequest { BatchId = Guid.NewGuid(), Reason = "A" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdjustInventory_Anonymous_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // Không có token

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/inventory/adjust", new AdjustInventoryRequest { BatchId = Guid.NewGuid(), Reason = "A" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdjustInventory_InvalidBody_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new AdjustInventoryRequest
        {
            BatchId = Guid.NewGuid(),
            NewQuantityBase = -5, // Giá trị âm không hợp lệ
            Reason = "" // Rỗng không hợp lệ
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/inventory/adjust", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdjustInventory_ReasonTooLong_ReturnsBadRequest()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new AdjustInventoryRequest
        {
            BatchId = Guid.NewGuid(),
            NewQuantityBase = 50,
            Reason = new string('A', 501) // 501 ký tự
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/inventory/adjust", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
