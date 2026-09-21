using System.Net;
using System.Net.Http.Json;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.Common;
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

public class MedicinesControllerIntegrationTests
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
        var response = await client.GetAsync($"/api/v1/medicines?search={keyword}&limit=20", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var body = await response.Content.ReadFromJsonAsync<List<MedicineResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var medicine = Assert.Single(body);
        Assert.Equal("Paracetamol 500mg", medicine.Name);
    }
    
    [Fact]
    public async Task SearchMedicines_AsPatient_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        // Act
        var response = await client.GetAsync("/api/v1/medicines?search=test", TestContext.Current.CancellationToken);

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
        var response = await client.GetAsync("/api/v1/medicines?search=test", TestContext.Current.CancellationToken);

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
        var response = await client.PatchAsync($"/api/v1/medicines/{id}/activate", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _medicineService.Verify(s => s.ActivateMedicineAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedMedicines_AsAdmin_WithStatusAndStockFilter_ReturnsOkAndCallsService()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var mockResult = new PagedResult<MedicineResponse>(
            new List<MedicineResponse>
            {
                new MedicineResponse { MedicineId = Guid.NewGuid(), Name = "Paracetamol", Status = "ACTIVE" }
            },
            1,
            10,
            1,
            1
        );

        _medicineService.Setup(s => s.GetPagedAsync(1, 10, "para", true, "ACTIVE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var response = await client.GetAsync("/api/v1/medicines/admin?page=1&pageSize=10&search=para&inStock=true&status=ACTIVE", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<MedicineResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(1, body.TotalItems);
        var medicine = Assert.Single(body.Items);
        Assert.Equal("Paracetamol", medicine.Name);

        _medicineService.Verify(s => s.GetPagedAsync(1, 10, "para", true, "ACTIVE", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedMedicines_AsPatient_ReturnsForbidden()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        // Act
        var response = await client.GetAsync("/api/v1/medicines/admin?status=ACTIVE", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPagedMedicines_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var app = CreateApp();
        var client = app.CreateClient(); // No token

        // Act
        var response = await client.GetAsync("/api/v1/medicines/admin?status=ACTIVE", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ==============================================
    // POST /api/v1/medicines
    // ==============================================

    [Fact]
    public async Task CreateMedicine_ReturnsBadRequest_WhenMissingUsageUnit()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new CreateMedicineRequest
        {
            Name = "Invalid Med",
            VolumePerBaseUnit = 500,
            UsageUnit = "   ",
            MedicineUnitId = Guid.NewGuid(),
            SalePrice = 100
        };

        _medicineService.Setup(s => s.CreateMedicineAsync(It.IsAny<CreateMedicineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng."));

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/medicines", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Vui lòng nhập Đơn vị dùng (Usage Unit)", content);
    }

    [Fact]
    public async Task CreateMedicine_ReturnsBadRequest_WhenMissingVolume()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var request = new CreateMedicineRequest
        {
            Name = "Invalid Med 2",
            UsageUnit = "ml",
            VolumePerBaseUnit = 0,
            MedicineUnitId = Guid.NewGuid(),
            SalePrice = 100
        };

        _medicineService.Setup(s => s.CreateMedicineAsync(It.IsAny<CreateMedicineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("Vui lòng nhập đúng Hàm lượng"));

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/medicines", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Vui lòng nhập đúng Hàm lượng", content);
    }

    // ==============================================
    // PUT /api/v1/medicines/{id}
    // ==============================================
    [Fact]
    public async Task UpdateMedicine_ReturnsOk_WhenRequestIsValid()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var id = Guid.NewGuid();
        var request = new UpdateMedicineRequest { Name = "Aspirin", UsageUnit = "mg", VolumePerBaseUnit = 500 };
        var mockResponse = new MedicineResponse { MedicineId = id, Name = "Aspirin" };
        
        _medicineService.Setup(s => s.UpdateMedicineAsync(It.IsAny<Guid>(), It.IsAny<UpdateMedicineRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMedicine_ReturnsBadRequest_WhenValidationFailed()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var id = Guid.NewGuid();
        var request = new UpdateMedicineRequest { Name = "Aspirin", UsageUnit = "ml", VolumePerBaseUnit = 0 };

        _medicineService.Setup(s => s.UpdateMedicineAsync(It.IsAny<Guid>(), It.IsAny<UpdateMedicineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("Vui lòng nhập đúng Hàm lượng"));

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Vui lòng nhập đúng Hàm lượng", content);
    }

    [Fact]
    public async Task UpdateMedicine_ReturnsBadRequest_WhenNameChanged()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var id = Guid.NewGuid();
        var request = new UpdateMedicineRequest { Name = "Aspirin Mod" };

        _medicineService.Setup(s => s.UpdateMedicineAsync(It.IsAny<Guid>(), It.IsAny<UpdateMedicineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("Tên thuốc là Master Data gốc, tuyệt đối không được sửa sau khi tạo."));

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Tên thuốc là Master Data gốc, tuyệt đối không được sửa sau khi tạo.", content);
    }

    [Fact]
    public async Task UpdateMedicine_ReturnsNotFound()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var id = Guid.NewGuid();
        var request = new UpdateMedicineRequest { Name = "Aspirin" };

        _medicineService.Setup(s => s.UpdateMedicineAsync(It.IsAny<Guid>(), It.IsAny<UpdateMedicineRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("Không tìm thấy thuốc."));

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
