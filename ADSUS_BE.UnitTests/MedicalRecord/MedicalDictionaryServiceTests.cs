using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class MedicalDictionaryServiceTests
{
    private readonly Mock<IMedicalDictionaryRepository> _repository = new();
    private readonly MedicalDictionaryService _sut;

    public MedicalDictionaryServiceTests() => _sut = new MedicalDictionaryService(_repository.Object);

    [Fact]
    public async Task GetDiseasesAsync_MapsAllFields()
    {
        // Arrange
        var diseaseId = Guid.NewGuid();
        var disease = new MedicalDisease
        {
            Id = diseaseId,
            Name = "Tiểu đường",
            RequiresNote = true,
            IsOther = false,
        };
        _repository.Setup(r => r.ListDiseasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalDisease> { disease });

        // Act
        var result = await _sut.GetDiseasesAsync();

        // Assert — field "Id" (không phải "DiseaseId") khớp đúng contract JSON FE đang đọc
        // (xem MedicalDictionaryDTOs.cs).
        var row = Assert.Single(result);
        Assert.Equal(diseaseId, row.Id);
        Assert.Equal("Tiểu đường", row.Name);
        Assert.True(row.RequiresNote);
        Assert.False(row.IsOther);
    }

    [Fact]
    public async Task GetDiseasesAsync_NoDiseases_ReturnsEmptyListNotNull()
    {
        // Arrange
        _repository.Setup(r => r.ListDiseasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalDisease>());

        // Act
        var result = await _sut.GetDiseasesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllergyTypesAsync_MapsAllFields()
    {
        // Arrange
        var allergyTypeId = Guid.NewGuid();
        var allergyType = new MedicalAllergyType
        {
            Id = allergyTypeId,
            Name = "Dị ứng thuốc kháng sinh",
            IsOther = false,
        };
        _repository.Setup(r => r.ListAllergyTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalAllergyType> { allergyType });

        // Act
        var result = await _sut.GetAllergyTypesAsync();

        // Assert
        var row = Assert.Single(result);
        Assert.Equal(allergyTypeId, row.Id);
        Assert.Equal("Dị ứng thuốc kháng sinh", row.Name);
        Assert.False(row.IsOther);
    }

    [Fact]
    public async Task GetAllergyTypesAsync_NoAllergyTypes_ReturnsEmptyListNotNull()
    {
        // Arrange
        _repository.Setup(r => r.ListAllergyTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicalAllergyType>());

        // Act
        var result = await _sut.GetAllergyTypesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
