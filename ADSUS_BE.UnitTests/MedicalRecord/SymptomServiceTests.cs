using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class SymptomServiceTests
{
    private readonly Mock<ISymptomCategoryRepository> _categories = new();
    private readonly SymptomService _sut;

    public SymptomServiceTests() => _sut = new SymptomService(_categories.Object);

    [Fact]
    public async Task GetCategoriesAsync_MapsCategoriesAndNestedSymptoms()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var symptomId = Guid.NewGuid();
        var category = new SymptomCategory
        {
            CategoryId = categoryId,
            Name = "Đau vú",
            IsOther = false,
            Symptoms = new List<Symptom>
            {
                new()
                {
                    SymptomId = symptomId,
                    CategoryId = categoryId,
                    Name = "Đau khi chạm",
                    IsOther = false,
                },
            },
        };
        _categories.Setup(r => r.GetAllWithSymptomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SymptomCategory> { category });

        // Act
        var result = await _sut.GetCategoriesAsync();

        // Assert
        var row = Assert.Single(result);
        Assert.Equal(categoryId, row.CategoryId);
        Assert.Equal("Đau vú", row.Name);
        Assert.False(row.IsOther);

        var symptom = Assert.Single(row.Symptoms);
        Assert.Equal(symptomId, symptom.SymptomId);
        Assert.Equal("Đau khi chạm", symptom.Name);
        Assert.False(symptom.IsOther);
    }

    [Fact]
    public async Task GetCategoriesAsync_NoCategories_ReturnsEmptyListNotNull()
    {
        // Arrange — UI render .map() trên kết quả; null sẽ làm vỡ màn tạo ca khám.
        _categories.Setup(r => r.GetAllWithSymptomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SymptomCategory>());

        // Act
        var result = await _sut.GetCategoriesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCategoriesAsync_CategoryWithNoSymptoms_ReturnsEmptySymptomsList()
    {
        // Arrange
        var category = new SymptomCategory
        {
            CategoryId = Guid.NewGuid(),
            Name = "Khác",
            IsOther = true,
            Symptoms = new List<Symptom>(),
        };
        _categories.Setup(r => r.GetAllWithSymptomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SymptomCategory> { category });

        // Act
        var result = await _sut.GetCategoriesAsync();

        // Assert
        var row = Assert.Single(result);
        Assert.True(row.IsOther);
        Assert.Empty(row.Symptoms);
    }
}
