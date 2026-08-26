using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.IntegrationTests.AppointmentScheduling;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;

namespace ADSUS_BE.IntegrationTests.PrescriptionAdherence;

public partial class MedicinesControllerIntegrationTests
{
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

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/medicines", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
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

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/medicines", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
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
        var existing = new Medicine { MedicineId = id, Name = "Aspirin" };
        var request = new UpdateMedicineRequest { Name = "Aspirin", UsageUnit = "mg", VolumePerBaseUnit = 500 };

        _medicines.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request);

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

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Vui lòng nhập đúng Hàm lượng", content);
    }

    [Fact]
    public async Task UpdateMedicine_ReturnsBadRequest_WhenNameChanged()
    {
        // Arrange
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Admin);

        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Name = "Aspirin" };
        var request = new UpdateMedicineRequest { Name = "Aspirin Mod" };

        _medicines.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
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

        _medicines.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Medicine)null);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/medicines/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
