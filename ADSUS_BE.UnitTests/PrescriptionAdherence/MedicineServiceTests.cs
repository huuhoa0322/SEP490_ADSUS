using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public class MedicineServiceTests
{
    private readonly Mock<IMedicineRepository> _medicineRepoMock;
    private readonly MedicineService _sut; // System Under Test

    public MedicineServiceTests()
    {
        _medicineRepoMock = new Mock<IMedicineRepository>();
        _sut = new MedicineService(_medicineRepoMock.Object);
    }

    [Fact]
    public async Task SearchMedicinesAsync_WithKeyword_ReturnsMappedResponses()
    {
        // Arrange
        var keyword = "para";
        var limit = 20;
        var medicines = new List<Medicine>
        {
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol 500mg" },
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paralmax" }
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
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Aspirin" }
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
}
