using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public class MedicineServiceTests
{
    private readonly Mock<IMedicineRepository> _medicineRepoMock;
    private readonly AppDbContext _db;
    private readonly MedicineService _sut; // System Under Test

    public MedicineServiceTests()
    {
        _medicineRepoMock = new Mock<IMedicineRepository>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new MedicineService(_medicineRepoMock.Object, _db);
    }

    [Fact]
    public async Task SearchMedicinesAsync_WithKeyword_ReturnsMappedResponses()
    {
        // Arrange
        var keyword = "para";
        var limit = 20;
        var medicines = new List<Medicine>
        {
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol 500mg", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow },
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paralmax", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow }
        };

        _medicineRepoMock.Setup(repo => repo.SearchByNameAsync(keyword, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicines);

        // Act
        var result = await _sut.SearchMedicinesAsync(keyword, limit, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var list = result as List<MedicineResponse> ?? new List<MedicineResponse>(result);
        Assert.Equal(2, list.Count);
        Assert.Equal("Paracetamol 500mg", list[0].Name);
        Assert.Equal("Paralmax", list[1].Name);
        
        _medicineRepoMock.Verify(repo => repo.SearchByNameAsync(keyword, limit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchMedicinesAsync_EmptyKeyword_ReturnsMappedResponses()
    {
        // Arrange
        var keyword = "";
        var limit = 5;
        var medicines = new List<Medicine>
        {
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Aspirin", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow }
        };

        _medicineRepoMock.Setup(repo => repo.SearchByNameAsync(keyword, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicines);

        // Act
        var result = await _sut.SearchMedicinesAsync(keyword, limit, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var list = result as List<MedicineResponse> ?? new List<MedicineResponse>(result);
        Assert.Single(list);
        Assert.Equal("Aspirin", list[0].Name);
    }
    [Fact]
    public async Task ActivateMedicineAsync_ValidId_SetsStatusToActive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Status = MedicineStatus.Inactive };

        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.ActivateMedicineAsync(id, CancellationToken.None);

        // Assert
        Assert.Equal(MedicineStatus.Active, existing.Status);
        _medicineRepoMock.Verify(repo => repo.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateMedicineAsync_InvalidId_ThrowsResourceNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ADSUS_BE.BLL.Common.Exceptions.ResourceNotFoundException>(
            () => _sut.ActivateMedicineAsync(id, CancellationToken.None)
        );
    }
}
